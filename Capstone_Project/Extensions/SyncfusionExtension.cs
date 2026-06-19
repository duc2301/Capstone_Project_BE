using Syncfusion.Licensing;

namespace Capstone_Project.Extensions
{
    public static class SyncfusionExtension
    {
        // Đăng ký Syncfusion Community license (dùng cho convert Office -> PDF).
        // Key đọc từ cấu hình "Syncfusion:LicenseKey"; rỗng -> bỏ qua (không crash).
        public static void RegisterSyncfusionLicense(this WebApplicationBuilder builder)
        {
            var licenseKey = builder.Configuration["Syncfusion:LicenseKey"];
            if (!string.IsNullOrWhiteSpace(licenseKey))
                SyncfusionLicenseProvider.RegisterLicense(licenseKey);
        }
    }
}
