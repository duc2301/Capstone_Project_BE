using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace Application.DTOs.RequestDTOs.NamingConvention
{
    public class CloneForFolderDTO
    {
        [Required]
        public Guid FolderId { get; set; }
    }
}
