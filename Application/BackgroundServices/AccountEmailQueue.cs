using System.Threading.Channels;
using Application.Interfaces.IBackgroundServices;

namespace Application.BackgroundServices
{
    public sealed class AccountEmailQueue : IAccountEmailQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(Guid accountId) => _channel.Writer.TryWrite(accountId);

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}
