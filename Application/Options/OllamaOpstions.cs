using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Options
{
    public class OllamaOptions
    {
        public string? BaseUrl { get; set; } 
        public string? EmbeddingModel { get; set; } 
        public string? ChatModel { get; set; }
        public string? SubModel { get; set; }
        public int? EmbeddingDimension { get; set; } 
    }
}
