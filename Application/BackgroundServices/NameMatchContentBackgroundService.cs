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
                    var ai = scope.ServiceProvider.GetRequiredService<IAIService>();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    // Tóm tắt nội dung file -> lưu vào FileItem.Description cho người dùng đọc nhanh.
                    // Không notification: tóm tắt là thông tin hỗ trợ, không phải cảnh báo.
                    var summary = await ai.SummarizeContentAsync(fileItemId, stoppingToken);
                    if (summary is null) continue; // không trích được chữ / AI lỗi -> giữ nguyên

                    var file = await uow.Repository<FileItem>().GetByIdAsync(fileItemId);
                    if (file is null) continue;

                    file.Description = summary;
                    await uow.CommitAsync();
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
