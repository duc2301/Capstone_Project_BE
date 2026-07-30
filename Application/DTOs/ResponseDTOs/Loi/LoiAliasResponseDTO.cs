namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiAliasResponseDTO
    {
        public Guid Id { get; set; }
        public string ParamNameInModel { get; set; } = string.Empty;
        public string StandardParamName { get; set; } = string.Empty;

        public bool IsSystemWide { get; set; }

        public DateTime? CreatedAt { get; set; }
    }
}
