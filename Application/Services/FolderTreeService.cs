using Application.DTOs.ResponseDTOs.Folder;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Entities;
using Domain.Enum.Cde;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class FolderTreeService : IFolderTreeService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FolderTreeService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }


        public async Task<List<FolderTreeNodeDTO>> GetTreeAsync(Guid projectId, CdeArea? area = null)
        {
            _ = await _unitOfWork.Repository<Project>().GetByIdAsync(projectId)
                ?? throw new ApiExceptionResponse("Project not found.", 404);

            var folders = (await _unitOfWork.Repository<Folder>().GetAllAsync())
                .Where(f => f.ProjectId == projectId && !f.IsTemplate)
                .Where(f => area == null || f.Area == area.Value)
                .ToList();

            var foldersById = folders.ToDictionary(f => f.Id);

            var visible = folders.ToList();
            var visibleIds = visible.Select(f => f.Id).ToHashSet();

            var nodes = visible.ToDictionary(f => f.Id, f => new FolderTreeNodeDTO
            {
                Id = f.Id,
                ProjectId = f.ProjectId,
                ParentFolderId = f.ParentFolderId,
                Name = f.Name,
                Area = f.Area
            });

            var roots = new List<FolderTreeNodeDTO>();
            foreach (var f in visible)
            {
                var node = nodes[f.Id];
                if (f.ParentFolderId.HasValue && visibleIds.Contains(f.ParentFolderId.Value))
                    nodes[f.ParentFolderId.Value].Children.Add(node);
                else
                    roots.Add(node);
            }

            SortRecursive(roots);
            return roots;
        }

        private static void SortRecursive(List<FolderTreeNodeDTO> nodes)
        {
            nodes.Sort((a, b) =>
            {
                var byArea = a.Area.CompareTo(b.Area);
                return byArea != 0 ? byArea : string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            foreach (var n in nodes) SortRecursive(n.Children);
        }
    }

}

