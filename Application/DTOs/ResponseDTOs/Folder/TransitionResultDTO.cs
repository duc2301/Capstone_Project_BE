using Domain.Enum.Cde;

namespace Application.DTOs.ResponseDTOs.Folder
{
    // Kết quả chuyển trạng thái: thư mục đích + số folder tạo + số file đã chuyển.
    public class TransitionResultDTO
    {
        public Guid TargetFolderId { get; set; }
        public CdeArea TargetArea { get; set; }
        public int FoldersCreated { get; set; }
        public int FilesPromoted { get; set; }
        public bool Moved { get; set; }   // true = MOVE (Published→Archived), false = COPY
    }
}
