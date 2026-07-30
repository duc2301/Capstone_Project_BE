using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Loi
{
    public class CreateLoiAliasDTO
    {
        // Tên như đang ghi trong file IFC.
        [Required]
        [MaxLength(200)]
        public string ParamNameInModel { get; set; } = string.Empty;

        // Tham số chuẩn muốn quy về.
        [Required]
        [MaxLength(200)]
        public string StandardParamName { get; set; } = string.Empty;
    }
}
