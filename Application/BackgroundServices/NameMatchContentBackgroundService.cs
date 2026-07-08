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
                    var notifier = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                    var result = await ai.CheckNameMatchesContentAsync(fileItemId, stoppingToken);

                    // Chỉ báo khi nghi lệch tên (advisory) — không spam khi khớp.
                    if (!result.Matches)
                    {
                        var file = await uow.Repository<FileItem>().GetByIdAsync(fileItemId);
                        if (file?.CreatedByAccountId is Guid uploader)
                            await notifier.NotifyAsync(
                                uploader,
                                $"Tên file \"{file.Name}\" có thể không khớp nội dung: {result.Reason}",
                                senderName: "AI kiểm tra",
                                linkType: "FileItem",
                                linkId: fileItemId.ToString());
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + "Name match content failed for FileItem {Id}.");
                }
            }
        }
    }
}
