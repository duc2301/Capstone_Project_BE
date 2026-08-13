using System.ComponentModel.DataAnnotations;
using Domain.Enum.Permission;

namespace Application.DTOs.RequestDTOs.PermissionMatrix
{
    // Lưu ma trận: chỉ gửi các ô người dùng thực sự thay đổi. projectId lấy từ route, không từ body.
    public class SavePermissionMatrixDTO
    {
        [Required]
        [MinLength(1)]
        public List<MatrixCellChangeDTO> Changes { get; set; } = new();
    }

    // Một ô thay đổi. Level: N/R/W cho thư mục; N/R/W/Inherit cho file (Inherit = xóa override).
    public class MatrixCellChangeDTO
    {
        [Required]
        public Guid TargetId { get; set; }

        [Required]
        public MatrixTargetType TargetType { get; set; }

        [Required]
        public Guid ProjectParticipantId { get; set; }

        [Required]
        public PermissionLevel Level { get; set; }
    }
}
