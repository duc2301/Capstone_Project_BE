using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Domain.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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

        public NameMatchContentBackgroundService(IServiceScopeFactory serviceScopeFactory)
        {
            _serviceScopeFactory = serviceScopeFactory;
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
                    if (analysis is null) continue; // không trích được chữ / AI lỗi -> giữ nguyên

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
                    Console.WriteLine($"Summarize content failed for FileItem {fileItemId}: {ex.Message}");
                }
            }
        }
    }
}
