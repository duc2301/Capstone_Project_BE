using System.ComponentModel.DataAnnotations;
using Domain.Enum.Loi;

namespace Application.DTOs.RequestDTOs.Loi
{
    public class RecomputeLoiCheckDTO
    {
        [EnumDataType(typeof(LoiStage), ErrorMessage = "Giai đoạn thiết kế không hợp lệ (chỉ nhận 2, 3, 4 hoặc 5).")]
        public LoiStage Stage { get; set; } = LoiStage.SchematicDesign;
    }
}
