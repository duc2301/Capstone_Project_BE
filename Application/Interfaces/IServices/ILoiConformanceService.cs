namespace Application.Interfaces.IServices
{
    public interface ILoiConformanceService
    {
        Task CheckAndSaveAsync(Guid fileVersionId, CancellationToken ct = default);
    }
}
