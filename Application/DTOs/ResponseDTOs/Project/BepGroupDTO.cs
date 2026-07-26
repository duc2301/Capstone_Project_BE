using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Project
{
    public class BepGroupDTO
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? PartnerOrganizationName { get; set; } // gợi ý đối tác cho nhóm (text)
    }
}
