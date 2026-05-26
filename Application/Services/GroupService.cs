using Application.DTOs.RequestDTOs.Group;
using Application.DTOs.ResponseDTOs.Group;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class GroupService : IGroupService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public GroupService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<GroupResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<GroupResponseDTO>>(
                await _unitOfWork.Repository<Group>().GetAllAsync());

        public async Task<GroupResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<GroupResponseDTO>(entity);
        }

        public async Task<GroupResponseDTO> CreateAsync(CreateGroupDTO dto)
        {
            var entity = _mapper.Map<Group>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Group>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<GroupResponseDTO>(entity);
        }

        public async Task<GroupResponseDTO> UpdateAsync(Guid id, UpdateGroupDTO dto)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Group>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<GroupResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Group>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Group with ID {id} not found.", 404);
            _unitOfWork.Repository<Group>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
