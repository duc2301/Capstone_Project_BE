using System.ComponentModel.DataAnnotations;

namespace Application.DTOs.RequestDTOs.Loi
{
    public class CreateLoiAliasDTO
    {
        [Required]
        [MaxLength(200)]
        public string ParamNameInModel { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string StandardParamName { get; set; } = string.Empty;
    }
}
