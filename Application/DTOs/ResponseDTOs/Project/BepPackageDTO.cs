using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Project
{
    public class BepPackageDTO
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public decimal? ContractValue { get; set; }
        public string? Currency { get; set; }
        public string? ContractorOrganizationName { get; set; }
    }
}
