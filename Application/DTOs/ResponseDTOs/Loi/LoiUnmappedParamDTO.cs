using System;
using System.Collections.Generic;
using System.Text;

namespace Application.DTOs.ResponseDTOs.Loi
{
    public class LoiUnmappedParamDTO
    {
        public string ParamNameInModel { get; set; } = string.Empty;
        public string SuggestedParamName { get; set; } = string.Empty;
        public string SuggestedParamNameNormalized { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public int ElementCount { get; set; }
    }
}
