using Application.DTOs.RequestDTOs.Permission;
using Application.DTOs.ResponseDTOs.Account;
using Application.DTOs.ResponseDTOs.Permission;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace Application.Services
{
    public class FilePermissionService : IFilePermissionService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public FilePermissionService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public Task<FilePermissionResponseDTO> CreateAsync(CreateFilePermissionDTO dto)
        {
            throw new NotImplementedException();
        }

        public Task DeleteAsync(Guid id)
        {
            throw new NotImplementedException();
        }

        public Task<IEnumerable<FilePermissionResponseDTO>> GetAllAsync()
        {
            throw new NotImplementedException();
        }

        public Task<FilePermissionResponseDTO?> GetByIdAsync(Guid id)
        {
            throw new NotImplementedException();
        }


        public Task<FilePermissionResponseDTO> UpdateAsync(Guid id, UpdateFilePermissionDTO dto)
        {
            throw new NotImplementedException();
        }

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> GetGroupFilePermissionResponsesAsync(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }

        public async Task<IEnumerable<GroupFilePermissionResponseDTO>> UpdatePermissionBulk(Guid fileItemId)
        {
            var items = await _unitOfWork.FilePermissionRepository.GetPartipatedGroupFilePermissionsByFileItemIdAsync(fileItemId);
            return _mapper.Map<IEnumerable<GroupFilePermissionResponseDTO>>(items);
        }
    }
}
