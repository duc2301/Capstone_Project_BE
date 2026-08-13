using System.Text.Json;
using Application.DTOs.ResponseDTOs.Loi;
using Application.ExceptionMiddleware;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public sealed class LoiCheckService : ILoiCheckService
    {
        private const string IfcFormat = "ifc";

        private readonly IUnitOfWork _uow;
        private readonly ILoiCheckQueue _queue;
        private readonly IPermissionCheckingService _permission;

        public LoiCheckService(IUnitOfWork uow, ILoiCheckQueue queue, IPermissionCheckingService permission)
        {
            _uow = uow;
            _queue = queue;
            _permission = permission;
        }

        public async Task<LoiCheckResponseDTO?> GetByFileItemAsync(
            Guid fileItemId, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            var fileItem = await RequireViewableFileAsync(fileItemId, actor, isSystemAdmin);
            if (fileItem.CurrentVersionId is null) return null;

            var check = (await _uow.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == fileItem.CurrentVersionId)).FirstOrDefault();
            return check is null ? null : Map(check);
        }

        public async Task<LoiCheckResponseDTO> RecomputeAsync(
            Guid fileItemId, LoiStage targetStage, Guid actor, bool isSystemAdmin, CancellationToken ct = default)
        {
            if (!System.Enum.IsDefined(targetStage))
                throw new ApiExceptionResponse("Giai đoạn thiết kế không hợp lệ.", 400);

            var fileItem = await RequireViewableFileAsync(fileItemId, actor, isSystemAdmin);
            if (fileItem.CurrentVersionId is null)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _uow.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);
            if (!string.Equals(version.Format, IfcFormat, StringComparison.OrdinalIgnoreCase))
                throw new ApiExceptionResponse("Chỉ đối chiếu thông tin phi hình học cho file .ifc.", 400);

            var now = DateTime.UtcNow;
            var check = (await _uow.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == version.Id)).FirstOrDefault();
            if (check is null)
            {
                check = new FileVersionLoiCheck { Id = Guid.NewGuid(), FileVersionId = version.Id, CreatedAt = now };
                await _uow.Repository<FileVersionLoiCheck>().CreateAsync(check);
            }
            else if (check.Status is LoiCheckStatus.Pending or LoiCheckStatus.Processing)
            {
                return Map(check);
            }

            check.Status = LoiCheckStatus.Pending;
            check.Verdict = LoiVerdict.None;
            check.TargetStage = targetStage;
            check.Error = null;
            check.UpdatedAt = now;
            await _uow.CommitAsync();

            _queue.Enqueue(version.Id);
            return Map(check);
        }

        private async Task<FileItem> RequireViewableFileAsync(Guid fileItemId, Guid actor, bool isSystemAdmin)
        {
            var fileItem = await _uow.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);

            if (!isSystemAdmin)
                await _permission.CanViewFileAsync(fileItemId, actor);

            return fileItem;
        }

        private static LoiCheckResponseDTO Map(FileVersionLoiCheck c) => new()
        {
            Status = c.Status,
            Verdict = c.Verdict,
            TargetStage = c.TargetStage,
            CoveragePercent = c.CoveragePercent,
            TotalElements = c.TotalElements,
            ConformantElements = c.ConformantElements,
            ElementsWithUnknownType = c.ElementsWithUnknownType,
            ElementsNotCoveredByStandard = c.ElementsNotCoveredByStandard,
            SchemaName = c.SchemaName,
            Error = c.Error,
            CheckedAt = c.CheckedAt,
            Missing = Deserialize<LoiMissingFieldDTO>(c.MissingSummaryJson),
            Unmapped = Deserialize<LoiUnmappedParamDTO>(c.UnmappedSummaryJson),
            NotCovered = Deserialize<LoiUncoveredComponentDTO>(c.NotCoveredSummaryJson),
            Sections = Deserialize<LoiSectionDTO>(c.SectionsJson)
        };

        private static List<T> Deserialize<T>(string? json) =>
            string.IsNullOrEmpty(json)
                ? new List<T>()
                : JsonSerializer.Deserialize<List<T>>(json) ?? new List<T>();
    }
}
