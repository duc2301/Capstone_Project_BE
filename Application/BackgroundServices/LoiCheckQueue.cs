using System.Threading.Channels;
using Application.Interfaces.IBackgroundServices;

namespace Application.BackgroundServices
{
    public sealed class LoiCheckQueue : ILoiCheckQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(Guid fileVersionId) => _channel.Writer.TryWrite(fileVersionId);

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}
