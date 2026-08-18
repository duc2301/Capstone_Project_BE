namespace Application.Interfaces.IServices
{
    // Niêm phong lưu trữ: chốt bản Published chính thức của 1 file vào vùng Archived.
    // KHÔNG nằm trong luồng phê duyệt — chỉ PM/Admin chủ động, bấm được nhiều lần (cộng dồn phiên bản).
    public interface IFileArchiveService
    {
        // Niêm phong bản Published hiện hành của file vào Archived.
        // Trả về Id của FileItem "bản lưu" trong Archived (tạo mới lần đầu, các lần sau cộng dồn version).
        Task<Guid> SealToArchiveAsync(Guid fileItemId, Guid actor, string actorRole);

        /// <summary>
        /// Niêm phong tự động khi duyệt yêu cầu trả file về WIP: bản Published sắp bị rút khỏi vùng
        /// chính thức nên phải chốt lại trước, nếu không tài liệu biến mất khỏi tra cứu ngữ nghĩa.
        /// Khác bản thủ công ở ba điểm: KHÔNG kiểm quyền PM/Admin (việc duyệt trả vùng đã ủy quyền cho
        /// Team Leader), KHÔNG ném lỗi khi không có gì để niêm phong (trả null), và KHÔNG commit —
        /// caller commit chung một lần với việc trả vùng.
        /// Trả về Id bản lưu nếu vừa niêm phong, null nếu bỏ qua (file không ở Published, chưa có bản
        /// Published, hoặc phiên bản này đã được niêm phong trước đó).
        /// </summary>
        Task<Guid?> SealForZoneReturnAsync(Guid fileItemId, Guid actorId);
    }
}
