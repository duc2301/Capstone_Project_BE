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
        private readonly IUnitOfWork _uow;
        private readonly ILoiCheckQueue _queue;

        public LoiCheckService(IUnitOfWork uow, ILoiCheckQueue queue)
        {
            _uow = uow;
            _queue = queue;
        }

        public async Task<LoiCheckResponseDTO?> GetByFileItemAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var fileItem = await _uow.Repository<FileItem>().GetByIdAsync(fileItemId);
            if (fileItem?.CurrentVersionId is null) return null;

            var check = (await _uow.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == fileItem.CurrentVersionId)).FirstOrDefault();
            return check is null ? null : Map(check);
        }

        public async Task<LoiCheckResponseDTO> RecomputeAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var fileItem = await _uow.Repository<FileItem>().GetByIdAsync(fileItemId)
                ?? throw new ApiExceptionResponse("File not found.", 404);
            if (fileItem.CurrentVersionId is null)
                throw new ApiExceptionResponse("File has no content version.", 404);

            var version = await _uow.Repository<FileVersionState>().GetByIdAsync(fileItem.CurrentVersionId.Value)
                ?? throw new ApiExceptionResponse("Current version not found.", 404);
            if (!string.Equals(version.Format, "ifc", StringComparison.OrdinalIgnoreCase))
                throw new ApiExceptionResponse("Chỉ kiểm LOI cho file .ifc.", 400);

            var now = DateTime.UtcNow;
            var check = (await _uow.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == version.Id)).FirstOrDefault();
            if (check is null)
            {
                check = new FileVersionLoiCheck { Id = Guid.NewGuid(), FileVersionId = version.Id, CreatedAt = now };
                await _uow.Repository<FileVersionLoiCheck>().CreateAsync(check);
            }
            check.Status = LoiCheckStatus.Pending;
            check.Verdict = LoiVerdict.None;
            check.Error = null;
            check.UpdatedAt = now;
            await _uow.CommitAsync();

            _queue.Enqueue(version.Id);
            return Map(check);
        }

        private static LoiCheckResponseDTO Map(FileVersionLoiCheck c) => new()
        {
            Status = c.Status,
            Verdict = c.Verdict,
            CoveragePercent = c.CoveragePercent,
            TotalElements = c.TotalElements,
            ConformantElements = c.ConformantElements,
            ElementsWithUnknownType = c.ElementsWithUnknownType,
            SchemaName = c.SchemaName,
            Error = c.Error,
            CheckedAt = c.CheckedAt,
            Missing = string.IsNullOrEmpty(c.MissingSummaryJson)
                ? new List<LoiMissingFieldDTO>()
                : JsonSerializer.Deserialize<List<LoiMissingFieldDTO>>(c.MissingSummaryJson) ?? new List<LoiMissingFieldDTO>()
        };
    }
}
