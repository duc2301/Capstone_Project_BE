using Application.DTOs.RequestDTOs.ProgressReport;
using Application.DTOs.ResponseDTOs.ProgressReport;
using Domain.Entities;

namespace Application.Interfaces.IServices
{
    public interface IProgressReportService
        : IGenericService<ProgressReport, CreateProgressReportDTO, UpdateProgressReportDTO, ProgressReportResponseDTO>
    {
    }
}
