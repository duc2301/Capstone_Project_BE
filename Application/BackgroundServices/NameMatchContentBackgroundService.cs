using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Channels;

namespace Application.BackgroundServices
{
    public class NameMatchContentBackgroundService : BackgroundService, INameMatchContentBackgroundService
    {
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<NameMatchContentBackgroundService> _logger;

        public NameMatchContentBackgroundService(IServiceScopeFactory serviceScopeFactory,
                                                 ILogger<NameMatchContentBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        public void Enqueue(Guid fileItemId) => _queue.Writer.TryWrite(fileItemId);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var fileItemId in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var _AIService = scope.ServiceProvider.GetRequiredService<IAIService>();
                    var _unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    // 1 lần gọi AI: tóm tắt + cờ nghi ngờ nội dung không liên quan (cho người kiểm tra).
                    // Ghi vào VERSION HIỆN HÀNH (per-version) -> khôi phục version cũ ra đúng mô tả/cảnh báo của nó.
                    var analysis = await _AIService.AnalyzeContentAsync(fileItemId, stoppingToken);
                    if (analysis is null)
                    {
                        // Giữ Warnning = null: CHƯA phân tích được (không trích được chữ / AI lỗi).
                        // Khác hẳn false = đã kiểm và không thấy lệch. FE dựa vào đúng ba trạng thái
                        // này để không hiển thị "sạch" cho một file thực ra chưa ai soi.
                        _logger.LogWarning("Không phân tích được nội dung FileItem {FileItemId} — giữ trạng thái chưa kiểm", fileItemId);
                        continue;
                    }

                    var version = await _unitOfWork.FileVersionRepository.GetCurrentStateAsync(fileItemId);
                    if (version is null) continue;

                    version.Description = analysis.Summary;
                    // Re-phân tích (bản mới) tự cập nhật cờ: khớp lại -> tắt cảnh báo cũ.
                    version.Warnning = analysis.Suspicious;
                    version.WarnningMessage = analysis.Suspicious
                        ? (string.IsNullOrWhiteSpace(analysis.Reason)
                            ? "Nội dung có thể không liên quan, cần người kiểm tra lại."
                            : analysis.Reason)
                        : null;
                    await _unitOfWork.CommitAsync();
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Trước ghi bằng Console.WriteLine nên lỗi không vào log pipeline —
                    // đúng loại hỏng hóc vô hình mà tính năng này hay gặp.
                    _logger.LogError(ex, "Phân tích nội dung thất bại cho FileItem {FileItemId}", fileItemId);
                }
            }
        }
    }
}
