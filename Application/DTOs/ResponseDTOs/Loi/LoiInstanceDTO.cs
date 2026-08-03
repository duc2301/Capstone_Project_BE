using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiInstanceDTO
    {
        public string? GlobalId { get; set; }

        public string? Name { get; set; }

        public string EntityType { get; set; } = string.Empty;

        public List<string> Details { get; set; } = new();
    }
}
