using Application.DTOs.RequestDTOs.ProgressReport;
using Application.DTOs.ResponseDTOs.ProgressReport;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using AutoMapper;
using Domain.Common;
using Domain.Entities;

namespace Application.Services
{
    public class ProgressReportService : IProgressReportService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public ProgressReportService(IUnitOfWork unitOfWork, IMapper mapper)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
        }

        public async Task<IEnumerable<ProgressReportResponseDTO>> GetAllAsync()
            => _mapper.Map<IEnumerable<ProgressReportResponseDTO>>(
                await _unitOfWork.Repository<ProgressReport>().GetAllAsync());

        public async Task<ProgressReportResponseDTO?> GetByIdAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ProgressReport>().GetByIdAsync(id);
            return entity == null ? null : _mapper.Map<ProgressReportResponseDTO>(entity);
        }

        public async Task<ProgressReportResponseDTO> CreateAsync(CreateProgressReportDTO dto)
        {
            var entity = _mapper.Map<ProgressReport>(dto);
            entity.Id = Guid.NewGuid();
            if (entity is IAuditable a) { var now = DateTime.UtcNow; a.CreatedAt = now; a.UpdatedAt = now; }
            await _unitOfWork.Repository<ProgressReport>().CreateAsync(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ProgressReportResponseDTO>(entity);
        }

        public async Task<ProgressReportResponseDTO> UpdateAsync(Guid id, UpdateProgressReportDTO dto)
        {
            var entity = await _unitOfWork.Repository<ProgressReport>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ProgressReport with ID {id} not found.", 404);
            _mapper.Map(dto, entity);
            if (entity is IAuditable a) a.UpdatedAt = DateTime.UtcNow;
            _unitOfWork.Repository<ProgressReport>().Update(entity);
            await _unitOfWork.CommitAsync();
            return _mapper.Map<ProgressReportResponseDTO>(entity);
        }

        public async Task DeleteAsync(Guid id)
        {
            var entity = await _unitOfWork.Repository<ProgressReport>().GetByIdAsync(id)
                ?? throw new ApiExceptionResponse($"ProgressReport with ID {id} not found.", 404);
            _unitOfWork.Repository<ProgressReport>().Delete(entity);
            await _unitOfWork.CommitAsync();
        }
    }
}
