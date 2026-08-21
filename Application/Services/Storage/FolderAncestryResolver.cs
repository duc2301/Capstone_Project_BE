using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;

namespace Application.Services.Storage
{
    public class FolderAncestryResolver : IFolderAncestryResolver
    {
        private readonly IUnitOfWork _unitOfWork;

        public FolderAncestryResolver(IUnitOfWork unitOfWork)
        {
            _unitOfWork = unitOfWork;
        }

        public async Task<IReadOnlyList<Folder>> GetChainAsync(Guid folderId, CancellationToken ct = default)
        {
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(folderId);
            if (folder == null) return Array.Empty<Folder>();

            // Nạp cả cây của dự án 1 lần rồi đi ngược trong bộ nhớ — rẻ hơn N truy vấn theo từng cấp.
            var byId = (await _unitOfWork.Repository<Folder>().FindAsync(f => f.ProjectId == folder.ProjectId))
                .ToDictionary(f => f.Id);

            return BuildChain(folder, byId);
        }

        /// <summary>
        /// Dựng chuỗi gốc -> lá từ một tập folder đã nạp sẵn. Tách static để caller nào đã có sẵn
        /// toàn bộ folder của dự án (vd đóng gói ZIP) dùng lại mà không phải truy vấn thêm lần nữa.
        /// </summary>
        public static IReadOnlyList<Folder> BuildChain(Folder leaf, IReadOnlyDictionary<Guid, Folder> byId)
        {
            var chain = new List<Folder>();
            // visited: nếu dữ liệu cây hỏng (parent trỏ vòng) thì dừng thay vì lặp vô hạn.
            var visited = new HashSet<Guid>();

            Folder? current = leaf;
            while (current is not null && visited.Add(current.Id))
            {
                chain.Add(current);
                current = current.ParentFolderId.HasValue && byId.TryGetValue(current.ParentFolderId.Value, out var parent)
                    ? parent
                    : null;
            }

            chain.Reverse();
            return chain;
        }
    }
}
