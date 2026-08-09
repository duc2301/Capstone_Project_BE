namespace Application.Interfaces.IBackgroundServices
{
    // Hàng đợi gửi email onboarding cho tài khoản vừa import (out-of-band, không chặn HTTP response).
    public interface IAccountEmailQueue
    {
        void Enqueue(Guid accountId);
        IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
    }
}
