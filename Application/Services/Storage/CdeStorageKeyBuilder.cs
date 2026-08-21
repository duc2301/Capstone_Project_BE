using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Cde;

namespace Application.Services.Storage
{
    public class CdeStorageKeyBuilder : ICdeStorageKeyBuilder
    {
        private const string ProjectsRoot = "projects";
        private const string DerivedSegment = "_derived";
        private const string IssuesSegment = "_issues";

        // Đủ để phân biệt hai dự án trùng tên, đủ ngắn để prefix còn đọc được.
        private const int IdSuffixLength = 8;

        // S3 giới hạn key 1024 byte. Chặn ở 700 cho phần prefix, chừa chỗ cho tên lá + hậu tố duy nhất.
        // Key sau khi chuẩn hoá chỉ còn ASCII nên số ký tự = số byte.
        private const int MaxPrefixLength = 700;

        // Đánh dấu prefix đã bị lược bớt cấp giữa vì cây quá sâu — để nhìn key là biết ngay,
        // không tưởng nhầm đó là đường dẫn đầy đủ.
        private const string TruncationMarker = "_luoc-bot";

        private const string FallbackProjectSegment = "du-an";
        private const string FallbackFolderSegment = "thu-muc";
        private const string FallbackDocumentStem = "tai-lieu";

        private readonly IUnitOfWork _unitOfWork;
        private readonly IFolderAncestryResolver _ancestry;

        public CdeStorageKeyBuilder(IUnitOfWork unitOfWork, IFolderAncestryResolver ancestry)
        {
            _unitOfWork = unitOfWork;
            _ancestry = ancestry;
        }

        public async Task<StorageObjectName> ForDocumentAsync(
            Guid folderId, string fileNameWithoutExtension, string? displayVersion, string extension,
            CancellationToken ct = default)
        {
            var chain = await RequireChainAsync(folderId, ct);
            var prefix = BuildPrefix(await BuildProjectSegmentAsync(chain[0].ProjectId), FolderSegments(chain));

            var name = Segment(fileNameWithoutExtension, FallbackDocumentStem);
            var version = FileDownloadNaming.ToKeySegment(displayVersion);
            var stem = version.Length == 0 ? name : $"{name}_{version}";

            return new StorageObjectName(prefix, stem, extension);
        }

        public async Task<StorageObjectName> ForDerivedAsync(
            Guid folderId, DerivedFileKind kind, string extension, CancellationToken ct = default)
        {
            // Tệp phái sinh không cần đường dẫn thư mục: nó không phải tài liệu người dùng thấy trong cây,
            // và gom phẳng theo dự án thì dọn dẹp/đối soát về sau dễ hơn.
            var projectId = await RequireProjectIdAsync(folderId);
            var prefix = BuildPrefix(await BuildProjectSegmentAsync(projectId), new[] { DerivedSegment, KindSegment(kind) });

            return new StorageObjectName(prefix, null, extension);
        }

        public async Task<StorageObjectName> ForIssueAttachmentAsync(
            Guid folderId, Guid issueId, string originalFileName, CancellationToken ct = default)
        {
            var projectId = await RequireProjectIdAsync(folderId);
            var prefix = BuildPrefix(
                await BuildProjectSegmentAsync(projectId),
                new[] { IssuesSegment, issueId.ToString("N") });

            var stem = Segment(Path.GetFileNameWithoutExtension(originalFileName), "dinh-kem");

            return new StorageObjectName(prefix, stem, Path.GetExtension(originalFileName));
        }

        // ---- Dựng prefix ----

        private async Task<IReadOnlyList<Folder>> RequireChainAsync(Guid folderId, CancellationToken ct)
        {
            var chain = await _ancestry.GetChainAsync(folderId, ct);
            if (chain.Count == 0)
                throw new ApiExceptionResponse("Folder not found.", 404);
            return chain;
        }

        private async Task<Guid> RequireProjectIdAsync(Guid folderId)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId)
                ?? throw new ApiExceptionResponse("Folder not found.", 404);
            return folder.ProjectId;
        }

        // "{ten-du-an}--{id8}": phần tên để đọc, phần id để không bao giờ đụng nhau khi trùng tên.
        private async Task<string> BuildProjectSegmentAsync(Guid projectId)
        {
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId);
            var name = Segment(project?.ProjectName, FallbackProjectSegment);

            return $"{name}--{ShortId(projectId)}";
        }

        // Thư mục gốc của mỗi vùng CDE được đánh số theo vòng đời (01-WIP, 02-Shared, …) thay vì để
        // xếp theo alphabet — nhìn bucket là thấy đúng thứ tự quy trình.
        private static IReadOnlyList<string> FolderSegments(IReadOnlyList<Folder> chain)
        {
            var segments = new List<string>(chain.Count);

            for (var index = 0; index < chain.Count; index++)
            {
                var folder = chain[index];
                var name = Segment(folder.Name, FallbackFolderSegment);

                segments.Add(index == 0 && folder.ParentFolderId == null
                    ? $"{AreaOrder(folder.Area)}-{name}"
                    : name);
            }

            return segments;
        }

        private static string BuildPrefix(string projectSegment, IReadOnlyList<string> tail)
        {
            var segments = new List<string> { ProjectsRoot, projectSegment };
            segments.AddRange(tail);

            // Cây quá sâu: bỏ dần các cấp Ở GIỮA, giữ dự án và thư mục lá (hai thứ định danh nhất).
            var truncated = false;
            while (segments.Count > 3 && JoinedLength(segments) > MaxPrefixLength)
            {
                segments.RemoveAt(2);
                truncated = true;
            }

            if (truncated) segments.Insert(2, TruncationMarker);

            var prefix = string.Join('/', segments);

            // Chốt chặn cuối: một segment đơn lẻ cũng có thể dài quá ngưỡng.
            return prefix.Length <= MaxPrefixLength
                ? prefix
                : prefix[..MaxPrefixLength].TrimEnd('/');
        }

        private static int JoinedLength(IReadOnlyList<string> segments)
            => segments.Sum(s => s.Length) + segments.Count - 1;

        // ---- Tiện ích ----

        private static string Segment(string? value, string fallback)
        {
            var segment = FileDownloadNaming.ToKeySegment(value);
            return segment.Length == 0 ? fallback : segment;
        }

        private static string ShortId(Guid id) => id.ToString("N")[..IdSuffixLength];

        private static string AreaOrder(CdeArea area) => ((int)area + 1).ToString("00");

        private static string KindSegment(DerivedFileKind kind) => kind switch
        {
            DerivedFileKind.Preview => "preview",
            DerivedFileKind.Signed => "signed",
            DerivedFileKind.Prepared => "prepared",
            _ => "other"
        };
    }
}
