using System.IO.Compression;
using System.Text;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Services.Storage;
using Domain.Entities;
using Domain.Enum.Audit;
using Domain.Enum.Cde;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    public class ProjectFileBundleService : IProjectFileBundleService
    {
        private const string BundleExtension = "zip";
        private const string BundleNamePrefix = "tai-lieu-du-an";
        private const int VietnamUtcOffsetHours = 7;
        private const string FailureReportEntryName = "TEP-KHONG-TAI-DUOC.txt";
        private const string FallbackProjectName = "du-an";

        private static readonly char[] InvalidNameChars =
            Path.GetInvalidFileNameChars().Concat(new[] { '/', '\\' }).Distinct().ToArray();

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFileStorageService _storage;
        private readonly IAuditLogService _auditLog;
        private readonly IWatermarkService _watermark;
        private readonly ILogger<ProjectFileBundleService> _logger;

        public ProjectFileBundleService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IAuditLogService auditLog,
            IWatermarkService watermark,
            ILogger<ProjectFileBundleService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _auditLog = auditLog;
            _watermark = watermark;
            _logger = logger;
        }

        public async Task<string> ResolveBundleFileNameAsync(Guid projectId, CancellationToken ct = default)
        {
            var project = await RequireProjectAsync(projectId);
            return FileDownloadNaming.BuildTimestampedName(
                BundleNamePrefix,
                project.ProjectName,
                DateTime.UtcNow.AddHours(VietnamUtcOffsetHours),
                BundleExtension);
        }

        public async Task WriteBundleAsync(
            Guid projectId, Guid actorId, Stream destination, CancellationToken ct = default)
        {
            var project = await RequireProjectAsync(projectId);
            var rootName = SanitizeName(project.ProjectName, FallbackProjectName);

            var folders = (await _unitOfWork.Repository<Folder>().FindAsync(f => f.ProjectId == projectId)).ToList();
            var folderPaths = BuildFolderPaths(folders, rootName);
            var entries = await BuildFileEntriesAsync(folders, folderPaths);

            await _auditLog.LogAndSaveAsync(
                LogScope.Project, AuditAction.Download, nameof(Project), projectId.ToString(), actorId,
                $"Tải toàn bộ tài liệu dự án ({entries.Count} tệp)", projectId);

            // Cùng 1 actor cho cả gói -> lấy tài khoản 1 lần, dùng chung cho mọi entry cần watermark.
            var actorAccount = await _unitOfWork.Repository<Account>().GetByIdAsync(actorId);

            var failures = new List<string>();

            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, true, Encoding.UTF8);

            foreach (var path in folderPaths.Values.OrderBy(p => p, StringComparer.Ordinal))
                archive.CreateEntry(path + "/");

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                await CopyIntoArchiveAsync(archive, entry, actorAccount, failures, ct);
            }

            if (failures.Count > 0)
                await WriteFailureReportAsync(archive, failures);
        }

        private async Task CopyIntoArchiveAsync(
            ZipArchive archive, BundleEntry entry, Account? actorAccount, List<string> failures, CancellationToken ct)
        {
            try
            {
                await using var source = await _storage.OpenReadAsync(entry.StoragePath, ct);
                var archiveEntry = archive.CreateEntry(entry.EntryName, CompressionLevel.Fastest);
                await using var target = archiveEntry.Open();

                // Watermark như /download — tránh tải cả gói để né watermark từng file.
                var isProtectedZone = entry.Area is CdeArea.Shared or CdeArea.Published or CdeArea.Archived;
                var watermarked = !isProtectedZone || actorAccount == null
                    ? source
                    : _watermark.Stamp(source, entry.Format, WatermarkLabelBuilder.Build(actorAccount));

                await watermarked.CopyToAsync(target, ct);
                if (!ReferenceEquals(watermarked, source))
                    await watermarked.DisposeAsync();
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Khong doc duoc noi dung tep {Entry} khi dong goi du an.", entry.EntryName);
                failures.Add($"{entry.EntryName} - {ex.Message}");
            }
        }

        private static async Task WriteFailureReportAsync(ZipArchive archive, List<string> failures)
        {
            var report = archive.CreateEntry(FailureReportEntryName, CompressionLevel.Fastest);
            await using var stream = report.Open();
            await using var writer = new StreamWriter(stream, new UTF8Encoding(true));
            await writer.WriteLineAsync("Các tệp sau không đọc được nội dung từ kho lưu trữ:");
            foreach (var failure in failures)
                await writer.WriteLineAsync(failure);
        }

        private async Task<List<BundleEntry>> BuildFileEntriesAsync(
            List<Folder> folders, Dictionary<Guid, string> folderPaths)
        {
            var folderIds = folders.Select(f => f.Id).ToHashSet();
            var areaByFolderId = folders.ToDictionary(f => f.Id, f => f.Area);
            var fileItems = (await _unitOfWork.Repository<FileItem>()
                    .FindAsync(f => folderIds.Contains(f.FolderId) && f.CurrentVersionId != null))
                .ToList();

            var versionIds = fileItems.Select(f => f.CurrentVersionId!.Value).ToHashSet();
            var versions = (await _unitOfWork.Repository<FileVersionState>()
                    .FindAsync(v => versionIds.Contains(v.Id)))
                .ToDictionary(v => v.Id);

            var entries = new List<BundleEntry>();
            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var fileItem in fileItems.OrderBy(f => f.Name, StringComparer.Ordinal))
            {
                if (!folderPaths.TryGetValue(fileItem.FolderId, out var folderPath)) continue;
                if (!versions.TryGetValue(fileItem.CurrentVersionId!.Value, out var version)) continue;
                if (string.IsNullOrEmpty(version.StoragePath)) continue;

                var fileName = SanitizeName(
                    FileDownloadNaming.BuildFileName(fileItem.Name, version.Format), fileItem.Id.ToString());
                var area = areaByFolderId.GetValueOrDefault(fileItem.FolderId);

                entries.Add(new BundleEntry(
                    MakeUniqueEntryName($"{folderPath}/{fileName}", usedNames),
                    version.StoragePath, version.Format ?? string.Empty, area));
            }

            return entries;
        }

        // Dùng chung phép duyệt cây với bộ dựng object key (FolderAncestryResolver), nhưng giữ cách
        // chuẩn hoá tên RIÊNG: tên bên trong file ZIP phải giữ tiếng Việt có dấu, khác với object key
        // vốn phải slug hoá về ASCII.
        private static Dictionary<Guid, string> BuildFolderPaths(List<Folder> folders, string rootName)
        {
            var byId = folders.ToDictionary(f => f.Id);
            var paths = new Dictionary<Guid, string>();

            foreach (var folder in folders)
            {
                var path = rootName;
                foreach (var node in FolderAncestryResolver.BuildChain(folder, byId))
                    path = $"{path}/{SanitizeName(node.Name, AreaFolderName(node.Area))}";

                paths[folder.Id] = path;
            }

            return paths;
        }

        private static string AreaFolderName(CdeArea area) => area switch
        {
            CdeArea.Wip => "WIP",
            CdeArea.Shared => "Shared",
            CdeArea.Published => "Published",
            CdeArea.Archived => "Archived",
            _ => "Other"
        };

        private static string MakeUniqueEntryName(string entryName, HashSet<string> usedNames)
        {
            if (usedNames.Add(entryName)) return entryName;

            var separatorIndex = entryName.LastIndexOf('/');
            var directory = separatorIndex < 0 ? string.Empty : entryName[..separatorIndex];
            var leaf = separatorIndex < 0 ? entryName : entryName[(separatorIndex + 1)..];
            var stem = Path.GetFileNameWithoutExtension(leaf);
            var extension = Path.GetExtension(leaf);

            for (var suffix = 2; ; suffix++)
            {
                var candidate = $"{directory}/{stem} ({suffix}){extension}";
                if (usedNames.Add(candidate)) return candidate;
            }
        }

        private static string SanitizeName(string? value, string fallback)
        {
            var trimmed = (value ?? string.Empty).Trim();
            if (trimmed.Length == 0) return fallback;

            var builder = new StringBuilder(trimmed.Length);
            foreach (var character in trimmed)
                builder.Append(InvalidNameChars.Contains(character) ? '_' : character);

            var cleaned = builder.ToString().Trim().TrimEnd('.');
            return cleaned.Length == 0 ? fallback : cleaned;
        }

        private async Task<Project> RequireProjectAsync(Guid projectId) =>
            await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
            ?? throw new ApiExceptionResponse("Project not found.", 404);

        private sealed record BundleEntry(string EntryName, string StoragePath, string Format, CdeArea Area);
    }
}
