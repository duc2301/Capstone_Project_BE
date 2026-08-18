using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.BackgroundServices
{
    /// <summary>
    /// Quét đối soát chỉ mục ngữ nghĩa: mỗi n phút tìm tệp đang ở vùng chính thức mà phiên bản hiện
    /// hành chưa có vector rồi đẩy vào hàng đợi ingest.
    /// Đây là lưới an toàn cho việc "có đường ghi mới quên gọi RequestIndexAsync" — trigger gắn ở
    /// từng service chỉ giúp giảm độ trễ, còn tính chắc chắn nằm ở vòng quét này.
    /// </summary>
    public class IndexReconcileBackgroundService : BackgroundService
    {
        private static readonly TimeSpan StartupDelay = TimeSpan.FromSeconds(30);

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<IndexReconcileBackgroundService> _logger;
        private readonly TimeSpan _interval;

        public IndexReconcileBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<IndexReconcileBackgroundService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;

            var minutes = configuration.GetValue("Rag:ReconcileIntervalMinutes", 15);
            _interval = TimeSpan.FromMinutes(minutes <= 0 ? 15 : minutes);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation(
                "IndexReconcileBackgroundService started. Sweeping every {Interval} minutes.",
                _interval.TotalMinutes);

            // Chờ app khởi động xong (DbContext/Ollama sẵn sàng) rồi mới quét lượt đầu.
            try
            {
                await Task.Delay(StartupDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var indexSync = scope.ServiceProvider.GetRequiredService<IDocumentIndexSyncService>();

                    var enqueued = await indexSync.SyncPendingAsync(stoppingToken);
                    if (enqueued > 0)
                        _logger.LogInformation(
                            "RAG reconcile: enqueued {Count} file(s) missing from the semantic index.", enqueued);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RAG reconcile sweep failed. Will retry next cycle.");
                }

                try
                {
                    await Task.Delay(_interval, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
