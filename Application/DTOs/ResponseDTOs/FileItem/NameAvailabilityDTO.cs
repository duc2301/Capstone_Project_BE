using Domain.Enum.Cde;
using Domain.Enum.File;

namespace Application.DTOs.ResponseDTOs.FileItem
{
    // Trả lời câu hỏi "tên này còn trống không, nếu bận thì bận ở đâu và tôi còn lựa chọn nào".
    // Dùng chung cho hai chỗ: hỏi TRƯỚC khi upload (để hỏi ý người dùng ngay trên modal, khỏi tải
    // xong hàng trăm MB mới báo trùng) và chốt tên khi người dùng chọn "tạo tài liệu riêng".
    public class NameAvailabilityDTO
    {
        // Tên đã hỏi (KHÔNG kèm đuôi) + đuôi file — echo lại để client ghép cặp với đúng tệp trong lô.
        public string Name { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;

        public NameConflictScope Scope { get; set; }

        public bool IsAvailable => Scope == NameConflictScope.None;

        // --- Tài liệu đang chiếm tên (null khi tên còn trống) ---
        public Guid? ConflictFileItemId { get; set; }
        public string? ConflictFolderName { get; set; }
        public CdeArea? ConflictArea { get; set; }
        public string? ConflictDisplayVersion { get; set; }

        // --- Lựa chọn còn lại cho người dùng ---

        // Lên phiên bản mới của tài liệu đang có. Chỉ đúng khi trùng trong CÙNG thư mục và tài liệu
        // đó không ở trạng thái Published (bản đã phát hành không nhận upload thay thế).
        public bool CanCreateVersion { get; set; }

        // Tách thành tài liệu riêng. Thư mục áp quy tắc đặt tên thì cùng bộ giá trị chỉ ra một tên,
        // nên không tách được — phải đổi giá trị trong quy tắc.
        public bool CanCreateNewDocument { get; set; }

        // Tên còn trống gần nhất theo kiểu "Tên (2)" — chỉ điền khi CanCreateNewDocument.
        public string? SuggestedName { get; set; }

        // Câu hướng dẫn cho ca người dùng không tự xử lý tại chỗ được (trùng ở khu vực khác,
        // tài liệu đang Published...). Null khi mọi lựa chọn đều nằm trong tầm tay họ.
        public string? Guidance { get; set; }
    }
}
