namespace Application.Interfaces.IServices
{
    // Niêm phong lưu trữ: chốt bản Published chính thức của 1 file vào vùng Archived.
    // KHÔNG nằm trong luồng phê duyệt — chỉ PM/Admin chủ động, bấm được nhiều lần (cộng dồn phiên bản).
    public interface IFileArchiveService
    {
        // Niêm phong bản Published hiện hành của file vào Archived.
        // Trả về Id của FileItem "bản lưu" trong Archived (tạo mới lần đầu, các lần sau cộng dồn version).
        Task<Guid> SealToArchiveAsync(Guid fileItemId, Guid actor, string actorRole);
    }
}
