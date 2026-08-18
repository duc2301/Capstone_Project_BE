using System.Text.Json;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Loi;
using Microsoft.Extensions.Logging;

namespace Application.Services.Loi
{
    public sealed class LoiConformanceService : ILoiConformanceService
    {
        private const string ParserName = "step-min";
        private const int MaxErrorLength = 500;

        private readonly IUnitOfWork _uow;
        private readonly ILoiCheckRepository _checks;
        private readonly IIfcLoiExtractor _extractor;
        private readonly IFileStorageService _storage;
        private readonly ILogger<LoiConformanceService> _logger;

        public LoiConformanceService(
            IUnitOfWork uow,
            ILoiCheckRepository checks,
            IIfcLoiExtractor extractor,
            IFileStorageService storage,
            ILogger<LoiConformanceService> logger)
        {
            _uow = uow;
            _checks = checks;
            _extractor = extractor;
            _storage = storage;
            _logger = logger;
        }

        public async Task CheckAndSaveAsync(Guid fileVersionId, CancellationToken ct = default)
        {
            var version = await _checks.GetVersionAsync(fileVersionId, ct);
            if (version is null || version.StoragePath is null)
            {
                _logger.LogWarning("Bỏ qua đối chiếu thông tin phi hình học: FileVersion {Id} không tồn tại hoặc chưa có nội dung.", fileVersionId);
                return;
            }

            var check = await _checks.GetCheckByFileVersionForUpdateAsync(fileVersionId, ct);
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
                var projectId = await _checks.GetProjectIdByFileItemAsync(version.FileItemId, ct);
                var ruleSetId = await ResolveRuleSetIdAsync(projectId, ct);

                var requirements = ruleSetId is null
                    ? (IReadOnlyList<LoiRequirement>)Array.Empty<LoiRequirement>()
                    : await _checks.GetRequirementsAsync(ruleSetId.Value, ct);

                var aliases = await _checks.GetAliasesForProjectAsync(projectId, ct);

                IfcLoiModel model;
                await using (var stream = await _storage.OpenReadAsync(version.StoragePath!, ct))
                    model = await _extractor.ExtractAsync(stream, ct);

                var components = ruleSetId is null
                    ? (IReadOnlyList<LoiComponent>)Array.Empty<LoiComponent>()
                    : await _checks.GetComponentsAsync(ruleSetId.Value, ct);

                var result = LoiEvaluator.Evaluate(model, requirements, aliases, components, check.TargetStage);

                check.Status = LoiCheckStatus.Done;
                check.Verdict = result.Verdict;
                check.CoveragePercent = result.CoveragePercent;
                check.TotalElements = result.TotalElements;
                check.ConformantElements = result.ConformantElements;
                check.ElementsWithUnknownType = result.ElementsWithUnknownType;
                check.ElementsNotCoveredByStandard = result.ElementsNotCoveredByStandard;
                check.SchemaName = model.SchemaName;
                check.MissingSummaryJson = JsonSerializer.Serialize(result.Missing);
                check.UnmappedSummaryJson = JsonSerializer.Serialize(result.Unmapped);
                check.NotCoveredSummaryJson = JsonSerializer.Serialize(result.NotCovered);
                check.SectionsJson = JsonSerializer.Serialize(result.Sections);
                check.CheckedAt = DateTime.UtcNow;
                check.UpdatedAt = check.CheckedAt.Value;
                await _uow.CommitAsync();
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                check.Status = LoiCheckStatus.Pending;
                await SaveStatusAsync(fileVersionId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Đối chiếu thông tin phi hình học thất bại cho FileVersion {Id}.", fileVersionId);
                check.Status = LoiCheckStatus.Failed;
                check.Verdict = LoiVerdict.Unknown;
                check.Error = Truncate(ex.Message, MaxErrorLength);
                check.UpdatedAt = DateTime.UtcNow;
                await SaveStatusAsync(fileVersionId);
            }
        }

        private async Task<Guid?> ResolveRuleSetIdAsync(Guid? projectId, CancellationToken ct)
        {
            if (projectId.HasValue)
            {
                var projectRuleSetId = await _checks.GetProjectRuleSetIdAsync(projectId.Value, ct);
                if (projectRuleSetId is not null)
                    return projectRuleSetId;
            }

            var fallbackId = await _checks.GetDefaultRuleSetIdAsync(ct);
            if (fallbackId is null)
                _logger.LogWarning("Chưa có bộ luật thông tin phi hình học mặc định — kết quả đối chiếu sẽ rỗng.");

            return fallbackId;
        }

        private async Task SaveStatusAsync(Guid fileVersionId)
        {
            try
            {
                await _uow.CommitAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Không lưu được trạng thái đối chiếu của FileVersion {Id}.", fileVersionId);
            }
        }

        private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max];
    }
}
