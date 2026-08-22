using Domain.Entities;

namespace Application.Interfaces.IServices
{
    // Đi ngược cây thư mục CDE. Tách riêng vì có ≥2 nơi cần cùng phép duyệt này nhưng định dạng
    // đầu ra khác nhau (gói ZIP giữ tên tiếng Việt có dấu, object key thì phải slug hoá) —
    // chỉ chia sẻ phần duyệt cây, không chia sẻ phần chuẩn hoá tên.
    public interface IFolderAncestryResolver
    {
        // Chuỗi thư mục từ GỐC -> folder đích (bao gồm chính nó). Rỗng nếu folder không tồn tại.
        Task<IReadOnlyList<Folder>> GetChainAsync(Guid folderId, CancellationToken ct = default);
    }
}
