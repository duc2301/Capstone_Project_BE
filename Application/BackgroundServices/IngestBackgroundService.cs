using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.Channels;


namespace Application.BackgroundServices
{
    public class IngestBackgroundService : BackgroundService, IIngestBackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>();

        public IngestBackgroundService(IServiceScopeFactory serviceScopeFactory)
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
                    var ingestService = scope.ServiceProvider.GetRequiredService<IDocumentIngestService>();
                    await ingestService.IngestFileAsync(fileItemId, stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message + "RAG ingest failed for FileItem {Id}.");
                }
            }
        }
    }
}
