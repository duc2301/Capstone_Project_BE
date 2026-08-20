using System.IO.Compression;
using System.Text;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
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
        private readonly ILogger<ProjectFileBundleService> _logger;

        public ProjectFileBundleService(
            IUnitOfWork unitOfWork,
            IFileStorageService storage,
            IAuditLogService auditLog,
            ILogger<ProjectFileBundleService> logger)
        {
            _unitOfWork = unitOfWork;
            _storage = storage;
            _auditLog = auditLog;
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

            var failures = new List<string>();

            using var archive = new ZipArchive(destination, ZipArchiveMode.Create, true, Encoding.UTF8);

            foreach (var path in folderPaths.Values.OrderBy(p => p, StringComparer.Ordinal))
                archive.CreateEntry(path + "/");

            foreach (var entry in entries)
            {
                ct.ThrowIfCancellationRequested();
                await CopyIntoArchiveAsync(archive, entry, failures, ct);
            }

            if (failures.Count > 0)
                await WriteFailureReportAsync(archive, failures);
        }

        private async Task CopyIntoArchiveAsync(
            ZipArchive archive, BundleEntry entry, List<string> failures, CancellationToken ct)
        {
            try
            {
                await using var source = await _storage.OpenReadAsync(entry.StoragePath, ct);
                var archiveEntry = archive.CreateEntry(entry.EntryName, CompressionLevel.Fastest);
                await using var target = archiveEntry.Open();
                await source.CopyToAsync(target, ct);
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

                entries.Add(new BundleEntry(
                    MakeUniqueEntryName($"{folderPath}/{fileName}", usedNames), version.StoragePath));
            }

            return entries;
        }

        private static Dictionary<Guid, string> BuildFolderPaths(List<Folder> folders, string rootName)
        {
            var byId = folders.ToDictionary(f => f.Id);
            var paths = new Dictionary<Guid, string>();

            foreach (var folder in folders.OrderBy(f => f.Area).ThenBy(f => f.Name, StringComparer.Ordinal))
                ResolvePath(folder, byId, paths, rootName);

            return paths;
        }

        private static string ResolvePath(
            Folder folder, Dictionary<Guid, Folder> byId, Dictionary<Guid, string> paths, string rootName)
        {
            var pending = new List<Folder>();
            var visited = new HashSet<Guid>();
            Folder? current = folder;

            while (current is not null && !paths.ContainsKey(current.Id) && visited.Add(current.Id))
            {
                pending.Add(current);
                current = current.ParentFolderId.HasValue && byId.TryGetValue(current.ParentFolderId.Value, out var parent)
                    ? parent
                    : null;
            }

            var path = current is not null && paths.TryGetValue(current.Id, out var known) ? known : rootName;

            for (var index = pending.Count - 1; index >= 0; index--)
            {
                path = $"{path}/{SanitizeName(pending[index].Name, AreaFolderName(pending[index].Area))}";
                paths[pending[index].Id] = path;
            }

            return path;
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

        private sealed record BundleEntry(string EntryName, string StoragePath);
    }
}
