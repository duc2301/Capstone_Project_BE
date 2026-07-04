using Application.DTOs.ResponseDTOs.Folder;
using Domain.Enum.Cde;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Interfaces.IServices
{
    public interface IFolderTreeService
    {
        Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, CdeArea? area = null);
    }
}
