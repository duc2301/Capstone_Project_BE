using Domain.Entities;

namespace Application.Services
{
    public static class WatermarkLabelBuilder
    {
        // Hệ thống lưu UTC; Việt Nam không có DST nên +7h cố định là đủ.
        public static string Build(Account account)
            => $"{account.UserName} · {account.Email} · {DateTime.UtcNow.AddHours(7):dd/MM/yyyy HH:mm}";
    }
}
