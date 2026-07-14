using System.Text.Json;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Loi;

namespace Application.Services.Loi
{
    public sealed class LoiConformanceService : ILoiConformanceService
    {
        private const string ParserName = "step-min";
        private const int MaxErrorLength = 500;

        private readonly IUnitOfWork _uow;
        private readonly IIfcLoiExtractor _extractor;
        private readonly IFileStorageService _storage;
        private readonly INotificationService _notifier;

        public LoiConformanceService(
            IUnitOfWork uow, IIfcLoiExtractor extractor, IFileStorageService storage, INotificationService notifier)
        {
            _uow = uow;
            _extractor = extractor;
            _storage = storage;
            _notifier = notifier;
        }

        public async Task CheckAndSaveAsync(Guid fileVersionId, CancellationToken ct = default)
        {
            var version = await _uow.Repository<FileVersion>().GetByIdAsync(fileVersionId);
            if (version is null) return;

            var check = (await _uow.Repository<FileVersionLoiCheck>()
                .FindAsync(c => c.FileVersionId == fileVersionId)).FirstOrDefault();
            var now = DateTime.UtcNow;
            if (check is null)
            {
                check = new FileVersionLoiCheck { Id = Guid.NewGuid(), FileVersionId = fileVersionId, CreatedAt = now };
                await _uow.Repository<FileVersionLoiCheck>().CreateAsync(check);
            }
            check.Status = LoiCheckStatus.Processing;
            check.ParserUsed = ParserName;
            check.Error = null;
            check.UpdatedAt = now;
            await _uow.CommitAsync();

            try
            {
                var requirements = (await _uow.Repository<LoiRequirement>()
                    .FindAsync(r => r.Discipline == LoiDiscipline.KienTrucKetCau)).ToList();
                var aliases = (await _uow.Repository<LoiFieldAlias>().GetAllAsync()).ToList();

                IfcLoiModel model;
                await using (var stream = await _storage.OpenReadAsync(version.StoragePath, ct))
                    model = await _extractor.ExtractAsync(stream, ct);

                var result = LoiEvaluator.Evaluate(model, requirements, aliases);

                check.Status = LoiCheckStatus.Done;
                check.Verdict = result.Verdict;
                check.CoveragePercent = result.CoveragePercent;
                check.TotalElements = result.TotalElements;
                check.ConformantElements = result.ConformantElements;
                check.ElementsWithUnknownType = result.ElementsWithUnknownType;
                check.SchemaName = model.SchemaName;
                check.MissingSummaryJson = JsonSerializer.Serialize(result.Missing);
                check.CheckedAt = DateTime.UtcNow;
                check.UpdatedAt = check.CheckedAt.Value;
                await _uow.CommitAsync();

                if (result.Verdict == LoiVerdict.Warning)
                    await NotifyUploaderAsync(version, result);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                check.Status = LoiCheckStatus.Pending;
                try { await _uow.CommitAsync(); } catch {}
            }
            catch (Exception ex)
            {
                check.Status = LoiCheckStatus.Failed;
                check.Verdict = LoiVerdict.Unknown;
                check.Error = Truncate(ex.Message, MaxErrorLength);
                check.UpdatedAt = DateTime.UtcNow;
                try { await _uow.CommitAsync(); } catch {}
            }
        }

        private async Task NotifyUploaderAsync(FileVersion version, LoiEvalResult result)
        {
            try
            {
                var fileItem = await _uow.Repository<FileItem>().GetByIdAsync(version.FileItemId);
                var recipient = version.UploadedByAccountId ?? fileItem?.CreatedByAccountId;
                if (recipient is not Guid account) return;

                var name = fileItem?.Name ?? "file";
                await _notifier.NotifyAsync(
                    account,
                    $"Kiểm LOI \"{name}\": còn cấu kiện thiếu trường thông tin phi hình học (đạt {result.CoveragePercent}%). Bấm để xem chi tiết.",
                    senderName: "Kiểm LOI",
                    linkType: "FileItem",
                    linkId: version.FileItemId.ToString());
            }
            catch
            {
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
