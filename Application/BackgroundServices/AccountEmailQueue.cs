using System.Threading.Channels;
using Application.Interfaces.IBackgroundServices;

namespace Application.BackgroundServices
{
    public sealed class AccountEmailQueue : IAccountEmailQueue
    {
        private readonly Channel<AccountEmailJob> _channel = Channel.CreateUnbounded<AccountEmailJob>(
            new UnboundedChannelOptions { SingleReader = true });

        public void Enqueue(Guid accountId, string? password) =>
            _channel.Writer.TryWrite(new AccountEmailJob(accountId, password));

        public IAsyncEnumerable<AccountEmailJob> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}
