using Domain.Enum.Loi;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiMissingFieldDTO
    {
        public string FieldName { get; set; } = string.Empty;
        public string? Variant { get; set; }
        public LoiParamGroup Group { get; set; }
        public LoiStage Stage { get; set; }
        public int MissingCount { get; set; }
    }
}
