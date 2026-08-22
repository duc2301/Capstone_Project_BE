namespace Application.DTOs.ResponseDTOs.Project
{
    public class ProjectImportGroupDTO
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public Guid? PartnerOrganizationId { get; set; }
        public string? PartnerOrganizationName { get; set; }
    }

    public class ProjectImportPackageDTO
    {
        public string? Code { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? WorkTypes { get; set; }
        public string? ScopeDescription { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public decimal? ContractValue { get; set; }
        public string? Currency { get; set; }
        public decimal? TaxRate { get; set; }
        public Guid? ContractorOrganizationId { get; set; }
        public string? ContractorOrganizationName { get; set; }
        public string? ContractNumber { get; set; }
        public string? ContractSignDate { get; set; }
        public Guid? RepresentativeAccountId { get; set; }
        public string? RepresentativeAccountName { get; set; }
        public string? ContractJobTitle { get; set; }
        public string? Notes { get; set; }
    }

    public class ProjectImportPreviewDTO
    {
        public string? ProjectName { get; set; }
        public string? ProjectCode { get; set; }
        public string? ProjectDescription { get; set; }
        public Guid? OwnerOrganizationId { get; set; }
        public string? OwnerOrganizationName { get; set; }
        public string? ContactAddress { get; set; }
        public string? Address { get; set; }
        public double? Latitude { get; set; }
        public double? Longitude { get; set; }
        public Guid? ManagerAccountId { get; set; }
        public string? ManagerAccountName { get; set; }
        public List<ProjectImportPackageDTO> Packages { get; set; } = new();
        public List<ProjectImportGroupDTO> Groups { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }
}
