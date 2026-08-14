using System.Threading.Channels;
using Application.Interfaces.IBackgroundServices;

namespace Application.BackgroundServices
{
    // Hàng đợi nền dựa trên System.Threading.Channels (không cần thư viện ngoài như Hangfire).
    // LƯU Ý: hàng đợi nằm trong RAM -> BE restart sẽ mất job đang chờ; ModelTranslationWorker quét lại
    // các bản Pending/Processing lúc khởi động để bù (xem worker).
    public sealed class ModelTranslationQueue : IModelTranslationQueue
    {
        private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(
            new UnboundedChannelOptions { SingleReader = false });

        public void Enqueue(Guid fileVersionId) => _channel.Writer.TryWrite(fileVersionId);

        public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct) =>
            _channel.Reader.ReadAllAsync(ct);
    }
}
