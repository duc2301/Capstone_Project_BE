using Application.DTOs.RequestDTOs.ProgressReport;
using Application.DTOs.ResponseDTOs.ProgressReport;
using Application.Interfaces.IServices;
using Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace Capstone_Project.Controllers
{
    [Route("api/progress-reports")]
    public class ProgressReportsController
        : BaseCrudController<ProgressReport, CreateProgressReportDTO, UpdateProgressReportDTO, ProgressReportResponseDTO>
    {
        public ProgressReportsController(
            IGenericService<ProgressReport, CreateProgressReportDTO, UpdateProgressReportDTO, ProgressReportResponseDTO> service)
            : base(service) { }
    }
}
