using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
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

        public NameMatchContentBackgroundService(Channel<Guid> queue, IServiceScopeFactory serviceScopeFactory)
        {
            _queue = queue;
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
                    var service = scope.ServiceProvider.GetRequiredService<IAIService>();
                    await service.CheckNameMatchesContentAsync(fileItemId, stoppingToken);
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
