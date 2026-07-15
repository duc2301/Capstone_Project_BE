using System;
using System.Collections.Generic;
using System.Text;

namespace Domain.Entities
{
    public class FileRelation
    {
        public Guid Id { get; set; }
        public Guid FileItemId { get; set; }
        public ICollection<FileItem> fileItems { get; set; }
    }
}
