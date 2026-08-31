namespace Application.Interfaces.IBackgroundServices
{
    // Một job gửi email onboarding cho tài khoản vừa import.
    // Password mang plaintext mật khẩu ngẫu nhiên để đưa vào email; có thể null khi job được
    // requeue lúc app khởi động lại (plaintext không lưu DB) -> khi đó email chỉ đưa link đặt mật khẩu.
    public readonly record struct AccountEmailJob(Guid AccountId, string? Password);

    // Hàng đợi gửi email onboarding cho tài khoản vừa import (out-of-band, không chặn HTTP response).
    public interface IAccountEmailQueue
    {
        void Enqueue(Guid accountId, string? password);
        IAsyncEnumerable<AccountEmailJob> ReadAllAsync(CancellationToken ct);
    }
}
