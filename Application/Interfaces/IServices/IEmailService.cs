namespace Application.Interfaces.IServices
{
    public interface IEmailService
    {
        // action != null -> hiển thị nút bấm (CTA) trong email. Tham số optional nên các caller cũ không đổi.
        Task SendEmailAsync(string to, string subject, string body, EmailAction? action = null);
    }

    // Nút hành động render trong email (vd: "Đặt mật khẩu" -> trang set password).
    public sealed record EmailAction(string Label, string Url);
}
