namespace Application.Interfaces.IServices
{
    // Khởi tạo cấu trúc thư mục CDE theo nghiệp vụ ISO 19650:
    //  - 4 khu vực gốc (WIP / Shared / Published / Archived) khi tạo dự án.
    //  - "Ô" thư mục con cho từng bên tham gia (Group) trong cả 4 khu vực.
    // Tất cả idempotent: gọi lại không tạo trùng.
    public interface IFolderBootstrapService
    {
        // Tạo 4 folder gốc cho dự án nếu chưa có.
        Task InitializeRootFoldersAsync(Guid projectId);

        // Tạo 4 thư mục con (1 cho mỗi khu vực) cho 1 Group tham gia dự án.
        // Tự đảm bảo 4 folder gốc tồn tại trước.
        Task ScaffoldParticipantFoldersAsync(Guid projectId, Guid groupId);
    }
}
