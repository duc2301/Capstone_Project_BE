using System.Collections.Concurrent;
using Application.DTOs.ResponseDTOs.Viewer;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    // Hosted service chạy nền: tiêu thụ ModelTranslationQueue, đẩy file IFC/CAD lên APS rồi dịch.
    // Vì chạy ở server (không gắn request) nên user reload/đóng tab không làm gián đoạn — đây là điểm mấu chốt.
    // Mỗi job tự tạo SCOPE mới (IServiceScopeFactory) để có DbContext/UnitOfWork riêng — KHÔNG dùng scope của request
    // (request đã kết thúc -> scope bị dispose -> lưu DB sẽ ObjectDisposedException).
    public sealed class ModelTranslationWorker : BackgroundService
    {
        private const int PollInitialDelayMs = 10_000;
        private const int PollMaxDelayMs = 60_000;
        private static readonly TimeSpan PollTimeout = TimeSpan.FromMinutes(30);
        private static readonly TimeSpan ForcedRestartGrace = TimeSpan.FromSeconds(60);
        private const int ConsumerCount = 3;
        private const int MaxErrorLength = 500;

        private readonly ConcurrentDictionary<Guid, byte> _inFlight = new();

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IModelTranslationQueue _queue;
        private readonly ILogger<ModelTranslationWorker> _logger;

        public ModelTranslationWorker(
            IServiceScopeFactory scopeFactory,
            IModelTranslationQueue queue,
            ILogger<ModelTranslationWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Chống mất job khi BE restart/crash: quét lại các bản còn dang dở rồi đẩy vào hàng đợi.
            await RequeueUnfinishedAsync(stoppingToken);

            var consumers = new Task[ConsumerCount];
            for (var i = 0; i < ConsumerCount; i++)
                consumers[i] = ConsumeAsync(stoppingToken);

            await Task.WhenAll(consumers);
        }

        private async Task ConsumeAsync(CancellationToken ct)
        {
            await foreach (var fileVersionId in _queue.ReadAllAsync(ct))
            {
                await ProcessAsync(fileVersionId, ct);
            }
        }

        // Lúc khởi động: Processing (đang dở khi tắt máy) -> đưa về Pending; rồi enqueue mọi Pending còn lại.
        private async Task RequeueUnfinishedAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                // Chỉ quét dòng version hiện hành — dòng lịch sử không cần dịch lại.
                var unfinished = (await uow.Repository<FileVersionState>()
                    .FindAsync(v => v.IsCurrent
                                && (v.ViewerStatus == ModelViewerStatus.Pending
                                 || v.ViewerStatus == ModelViewerStatus.Processing))).ToList();

                foreach (var v in unfinished)
                {
                    if (v.ViewerStatus == ModelViewerStatus.Processing)
                        v.ViewerStatus = ModelViewerStatus.Pending;   // bản dở -> dịch lại từ đầu
                }
                if (unfinished.Count > 0) await uow.CommitAsync();

                foreach (var v in unfinished) _queue.Enqueue(v.Id);

                if (unfinished.Count > 0)
                    _logger.LogInformation("Re-enqueued {Count} unfinished model translation job(s) on startup.", unfinished.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-enqueue unfinished model translation jobs on startup.");
            }
        }

        private async Task ProcessAsync(Guid fileVersionId, CancellationToken ct)
        {
            if (!_inFlight.TryAdd(fileVersionId, 0)) return;
            try
            {
                await ProcessCoreAsync(fileVersionId, ct);
            }
            finally
            {
                _inFlight.TryRemove(fileVersionId, out _);
            }
        }

        private async Task ProcessCoreAsync(Guid fileVersionId, CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var viewer = scope.ServiceProvider.GetRequiredService<IViewerService>();
            var storage = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

            FileVersionState? version = null;
            try
            {
                version = await uow.Repository<FileVersionState>().GetByIdAsync(fileVersionId);
                if (version == null)
                {
                    _logger.LogWarning("Model translation skipped: FileVersionState {Id} not found.", fileVersionId);
                    return;
                }

                // Idempotent: đã Ready và có URN thì thôi (vd bị enqueue trùng).
                if (version.ViewerStatus == ModelViewerStatus.Ready && !string.IsNullOrWhiteSpace(version.ViewerUrn))
                    return;

                version.ViewerStatus = ModelViewerStatus.Processing;
                version.ViewerError = null;
                await uow.CommitAsync();

                string? urn = null;
                var forcedRestart = false;

                if (!string.IsNullOrWhiteSpace(version.ViewerUrn))
                {
                    var existing = version.ViewerUrn;
                    var current = await viewer.GetStatusAsync(existing, ct);

                    if (current.Status == TranslationStatuses.Success)
                    {
                        version.ViewerProgress = current.Progress;
                        version.ViewerStatus = ModelViewerStatus.Ready;
                        await uow.CommitAsync();
                        return;
                    }

                    if (current.Status != TranslationStatuses.NotFound)
                    {
                        urn = existing;
                        if (current.Status is TranslationStatuses.Failed or TranslationStatuses.Timeout)
                        {
                            await viewer.RetranslateAsync(existing, ct);
                            forcedRestart = true;
                        }
                    }
                }

                if (urn is null)
                {
                    // Tên file phải kèm ĐÚNG đuôi (rvt/ifc/nwd…) để APS nhận diện định dạng nguồn.
                    var fileItem = await uow.Repository<FileItem>().GetByIdAsync(version.FileItemId);
                    var baseName = fileItem?.Name ?? "model";
                    var fileName = $"{baseName}.{version.Format}";

                    await using (var stream = await storage.OpenReadAsync(version.StoragePath!, ct))
                    {
                        var translated = await viewer.UploadAndTranslateAsync(stream, fileName, version.FileSizeBytes ?? 0, ct);
                        urn = translated.Urn;
                    }

                    // Lưu URN sớm (ngay khi job dịch đã được khởi tạo) — /view có thể mount viewer trong lúc còn dịch.
                    version.ViewerUrn = urn;
                    await uow.CommitAsync();
                }

                await PollUntilDoneAsync(uow, viewer, version, urn, forcedRestart, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                // Host đang dừng — để Pending lại để lần khởi động sau quét tiếp.
                if (version != null)
                {
                    version.ViewerStatus = ModelViewerStatus.Pending;
                    try { await uow.CommitAsync(); } catch { /* host đang tắt */ }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Model translation failed for FileVersion {Id}.", fileVersionId);
                if (version != null)
                {
                    version.ViewerStatus = ModelViewerStatus.Failed;
                    version.ViewerError = Truncate(ex.Message, MaxErrorLength);
                    try { await uow.CommitAsync(); } catch (Exception saveEx) { _logger.LogError(saveEx, "Failed to persist Failed status for FileVersion {Id}.", fileVersionId); }
                }
            }
        }

        private static async Task PollUntilDoneAsync(
            IUnitOfWork uow, IViewerService viewer, FileVersionState version, string urn,
            bool forcedRestart, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            var deadline = now.Add(PollTimeout);
            var acceptTerminalFrom = forcedRestart ? now.Add(ForcedRestartGrace) : now;
            var delayMs = PollInitialDelayMs;

            while (!ct.IsCancellationRequested)
            {
                await Task.Delay(delayMs, ct);
                delayMs = Math.Min(delayMs * 2, PollMaxDelayMs);

                var status = await viewer.GetStatusAsync(urn, ct);
                version.ViewerProgress = status.Progress;

                if (status.Status == TranslationStatuses.Success)
                {
                    version.ViewerStatus = ModelViewerStatus.Ready;
                    await uow.CommitAsync();
                    return;
                }

                var failedAtAps = status.Status is TranslationStatuses.Failed or TranslationStatuses.Timeout
                    && DateTime.UtcNow >= acceptTerminalFrom;

                if (failedAtAps || DateTime.UtcNow >= deadline)
                {
                    version.ViewerStatus = ModelViewerStatus.Failed;
                    version.ViewerError = failedAtAps
                        ? $"APS translation {status.Status}."
                        : $"APS translation did not finish within {PollTimeout.TotalMinutes:0} minutes.";
                    await uow.CommitAsync();
                    return;
                }

                await uow.CommitAsync();
            }
        }

        private static string Truncate(string value, int max) =>
            value.Length <= max ? value : value[..max];
    }
}
