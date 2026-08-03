using Domain.Enum.Loi;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiRuleResultDTO
    {
        public string Code { get; set; } = string.Empty;

        public LoiSeverity Severity { get; set; }

        public int OccurrenceCount { get; set; }

        public bool Truncated { get; set; }

        public List<LoiInstanceDTO> Instances { get; set; } = new();
    }
}
