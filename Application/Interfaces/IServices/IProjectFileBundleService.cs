namespace Application.Interfaces.IServices
{
    public interface IProjectFileBundleService
    {
        Task<string> ResolveBundleFileNameAsync(Guid projectId, CancellationToken ct = default);

        Task WriteBundleAsync(Guid projectId, Guid actorId, Stream destination, CancellationToken ct = default);
    }
}
