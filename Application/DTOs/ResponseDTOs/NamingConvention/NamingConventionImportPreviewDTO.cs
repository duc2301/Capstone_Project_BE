using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.NamingConvention
{
    public class NamingConventionImportPreviewDTO
    {
        public List<ImportedNamingFieldDTO> Fields { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
    }

    public class ImportedNamingFieldDTO
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
        public List<ImportedNamingValueDTO> Values { get; set; } = new();
    }

    public class ImportedNamingValueDTO
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int OrderIndex { get; set; }
    }
}
