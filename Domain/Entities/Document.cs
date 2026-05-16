using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class Document
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public string Author { get; set; }
        public string Type { get; set; }
    }
}
