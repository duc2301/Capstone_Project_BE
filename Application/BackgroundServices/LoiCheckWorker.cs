using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Domain.Enum.Loi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    public sealed class LoiCheckWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILoiCheckQueue _queue;
        private readonly ILogger<LoiCheckWorker> _logger;

        public LoiCheckWorker(
            IServiceScopeFactory scopeFactory,
            ILoiCheckQueue queue,
            ILogger<LoiCheckWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _queue = queue;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await RequeueUnfinishedAsync(stoppingToken);

            await foreach (var fileVersionId in _queue.ReadAllAsync(stoppingToken))
            {
                await ProcessAsync(fileVersionId, stoppingToken);
            }
        }

        private async Task ProcessAsync(Guid fileVersionId, CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var conformance = scope.ServiceProvider.GetRequiredService<ILoiConformanceService>();
                await conformance.CheckAndSaveAsync(fileVersionId, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "LOI check failed for FileVersion {Id}.", fileVersionId);
            }
        }

        private async Task RequeueUnfinishedAsync(CancellationToken ct)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                var unfinished = (await uow.Repository<FileVersionLoiCheck>()
                    .FindAsync(c => c.Status == LoiCheckStatus.Pending
                                 || c.Status == LoiCheckStatus.Processing)).ToList();

                foreach (var c in unfinished)
                    if (c.Status == LoiCheckStatus.Processing)
                        c.Status = LoiCheckStatus.Pending;

                if (unfinished.Count > 0) await uow.CommitAsync();

                foreach (var c in unfinished) _queue.Enqueue(c.FileVersionId);

                if (unfinished.Count > 0)
                    _logger.LogInformation("Re-enqueued {Count} unfinished LOI check(s) on startup.", unfinished.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to re-enqueue unfinished LOI checks on startup.");
            }
        }
    }
}
