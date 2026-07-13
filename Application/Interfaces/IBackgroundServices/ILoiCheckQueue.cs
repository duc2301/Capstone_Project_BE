namespace Application.Interfaces.IBackgroundServices
{
    public interface ILoiCheckQueue
    {
        void Enqueue(Guid fileVersionId);
        IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
    }
}
