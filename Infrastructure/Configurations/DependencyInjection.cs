using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Mapping;
using Application.Services;
using Infrastructure.DbContexts;
using Infrastructure.UnitOfWorks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Infrastructure.Configurations
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddInfrastructureService(
            this IServiceCollection services,
            IConfiguration configuration)
        {
            var connectionString = configuration.GetConnectionString("DefaultConnection");

            services.AddDbContext<CDESystemDbContext>(options =>
                options.UseNpgsql(connectionString)
            );

            services.AddAutoMapper(cfg => cfg.AddProfile<MappingProfile>());

            services.AddScoped<IUnitOfWork, UnitOfWork>();

            // CRUD per-entity services
            services.AddScoped<IAccountService, AccountService>();
            services.AddScoped<IOrganizationTypeService, OrganizationTypeService>();
            services.AddScoped<IOrganizationService, OrganizationService>();
            services.AddScoped<IGroupService, GroupService>();
            services.AddScoped<IProjectService, ProjectService>();
            services.AddScoped<IContractPackageService, ContractPackageService>();
            services.AddScoped<IFolderService, FolderService>();
            services.AddScoped<IFolderBootstrapService, FolderBootstrapService>();
            services.AddScoped<IFolderPermissionService, FolderPermissionService>();
            services.AddScoped<IFileItemService, FileItemService>();
            services.AddScoped<IApprovalService, ApprovalService>();
            // Kho file: chọn provider qua "FileStorage:Provider" (Local mặc định | ViettelS3).
            var storageProvider = configuration["FileStorage:Provider"] ?? "Local";
            if (storageProvider.Equals("ViettelS3", StringComparison.OrdinalIgnoreCase)
                || storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IFileStorageService, ViettelS3FileStorageService>();
            else
                services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IFileUploadService, FileUploadService>();
            services.AddScoped<ISubmittalService, SubmittalService>();
            services.AddScoped<IDiscussionService, DiscussionService>();
            services.AddScoped<IIssueService, IssueService>();
            services.AddScoped<IContractService, ContractService>();
            services.AddScoped<IProjectModelService, ProjectModelService>();
            services.AddScoped<IModelFileService, ModelFileService>();


            // Auth (giống ChemXLab) + refresh token
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, GmailEmailService>();

            // Profile self-service (GET/PUT/change-password trên chính user hiện tại)
            services.AddScoped<IProfileService, ProfileService>();

            // Notification dispatcher (event -> tạo Notification rows)
            services.AddScoped<INotificationService, NotificationService>();

            // Invitation flow: Manager mời account vô dự án -> accept tạo ProjectParticipant
            services.AddScoped<IInvitationService, InvitationService>();

            // Project flow (custom, ngoài CRUD generic): Admin tạo PM cho project, PM add bên tham gia
            services.AddScoped<IProjectFlowService, ProjectFlowService>();

            services.AddMemoryCache();
            services.AddHttpClient<IViewerService, ViewerService>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config["Aps:BaseUrl"] ?? "https://developer.api.autodesk.com";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(10);   // upload file CAD/BIM lớn
            });

            return services;
        }
    }
}
