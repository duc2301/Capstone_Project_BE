namespace Application.DTOs.ResponseDTOs.ContractPackage
{
    public class ContractPackagePageDTO
    {
        public List<ContractPackageResponseDTO> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
