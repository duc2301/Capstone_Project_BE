using Application.DTOs.RequestDTOs.Issue;
using Application.DTOs.ResponseDTOs.Issue;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class IssueService : IIssueService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public IssueService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<IssueResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<IssueResponseDTO>>(
                await _unitOfWork.Repository<Issue>().GetAllAsync());

        public async Task<IssueResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<IssueResponseDTO>(entity);
        }

        public async Task<IssueResponseDTO> CreateAsync(CreateIssueDTO dto)
        {
            var entity = _mapper.Map<Issue>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<Issue>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<IssueResponseDTO>(entity);
        }

        public async Task<IssueResponseDTO> UpdateAsync(Guid id, UpdateIssueDTO dto)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Issue with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<Issue>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<IssueResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<Issue>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"Issue with ID {id} not found.", 404);
            _unitOfWork.Repository<Issue>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
