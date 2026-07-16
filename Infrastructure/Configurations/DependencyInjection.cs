using Application.BackgroundServices;
using Application.Interfaces.IBackgroundServices;
using Application.Interfaces.IRepositories;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Mapping;
using Application.Options;
using Application.Services;
using Infrastructure.DbContexts;
using Infrastructure.Repositories;
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

            services.Configure<VnptSmartCaOptions>(configuration.GetSection("VnptSmartCA"));

            services.AddDbContext<CDESystemDbContext>(options =>
                options.UseNpgsql(connectionString, x => x.UseVector())
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
            services.AddScoped<IFileZoneResolverService, FileZoneResolverService>();
            services.AddScoped<IFileItemService, FileItemService>();
            services.AddScoped<IFileLinkService, FileLinkService>();
            services.AddScoped<IApprovalService, ApprovalService>();
            services.AddScoped<IFileSignaturePositionService, FileSignaturePositionService>();
            services.AddScoped<IPdfSignatureService, PdfSignatureService>();
            services.AddScoped<IVnptSmartCaService, VnptSmartCaService>();
            services.AddScoped<IZoneReturnRequestService, ZoneReturnRequestService>();
            // Kho file: chọn provider qua "FileStorage:Provider" (Local mặc định | ViettelS3).
            var storageProvider = configuration["FileStorage:Provider"] ?? "Local";
            if (storageProvider.Equals("ViettelS3", StringComparison.OrdinalIgnoreCase)
                || storageProvider.Equals("S3", StringComparison.OrdinalIgnoreCase))
                services.AddSingleton<IFileStorageService, ViettelS3FileStorageService>();
            else
                services.AddSingleton<IFileStorageService, LocalFileStorageService>();
            services.AddScoped<IFileUploadService, FileUploadService>();
            // Naming convention: cấu hình quy ước đặt tên file + sinh tên khi upload
            services.AddScoped<INamingConventionService, NamingConventionService>();
            services.AddSingleton<IOfficeToPdfConverter, SyncfusionOfficeToPdfConverter>();
            services.AddScoped<IFileViewService, FileViewService>();
            services.AddScoped<IMarkupService, MarkupService>();
            services.AddScoped<IDiscussionService, DiscussionService>();
            services.AddScoped<IIssueService, IssueService>();
            services.AddScoped<IContractService, ContractService>();
            services.AddScoped<IFilePermissionService, FilePermissionService>();
            services.AddScoped<IFolderPermissionService, FolderPermissionService>();
            services.AddScoped<IFolderTreeService, FolderTreeService>();
            services.AddScoped<IFolderTreeRepository, FolderTreeRepository>();

            // Centralized permission checking (baseline) — features call this instead of ad-hoc checks
            services.AddScoped<IPermissionCheckingService, PermissionCheckingService>();
            services.AddScoped<IPermissionCheckingRepository, PermissionCheckingRepository>();

            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();
            services.AddScoped<IEmailService, GmailEmailService>();
            services.AddScoped<IEmbeddingService, EmbeddingService>();
            services.AddScoped<IFileContentReader, FileContentReader>();
            services.AddScoped<IAIService, AIService>();
            services.AddScoped<IDocumentIngestService, DocumentIngestService>();
            services.AddScoped<IChunkContextEnricher, ChunkContextEnricher>();
            services.AddScoped<IDocumentSearchRepository, DocumentSearchRepository>();
            services.AddScoped<ISemanticSearchService, SemanticSearchService>();

            // Profile self-service (GET/PUT/change-password trên chính user hiện tại)
            services.AddScoped<IProfileService, ProfileService>();

            // Notification dispatcher (event -> tạo Notification rows)
            services.AddScoped<INotificationService, NotificationService>();

            // Repository cho Background Service digest
            services.AddScoped<INotificationDigestRepository, NotificationDigestRepository>();

            // Background Service: Gửi email digest thông báo chưa đọc (business logic ở Application)
            services.AddHostedService<Application.BackgroundServices.NotificationEmailDigestBackgroundService>();
            services.AddHostedService(sp => sp.GetRequiredService<IngestBackgroundService>());
            services.AddHostedService(sp => sp.GetRequiredService<NameMatchContentBackgroundService>());

            // Invitation flow: Manager mời account vô dự án -> accept tạo ProjectParticipant
            services.AddScoped<IInvitationService, InvitationService>();

            // Project flow (custom, ngoài CRUD generic): Admin tạo PM cho project, PM add bên tham gia
            services.AddScoped<IProjectFlowService, ProjectFlowService>();

            services.Configure<OllamaOptions>(configuration.GetSection("Ollama"));

            // Hàng đợi dịch model nền (singleton: producer upload/view + consumer ModelTranslationWorker dùng chung).
            // Worker (BackgroundService) đăng ký ở Program.cs (host) vì cần Microsoft.Extensions.Hosting.
            services.AddSingleton<IModelTranslationQueue, ModelTranslationQueue>();

            // Kiểm LOI (thông tin phi hình học, BXD-347): parser STEP + engine + hàng đợi nền + read service.
            // Worker (LoiCheckWorker) đăng ký ở Program.cs.
            services.AddSingleton<IIfcLoiExtractor, Application.Services.Loi.IfcStepPropertyExtractor>();
            services.AddSingleton<ILoiCheckQueue, LoiCheckQueue>();
            services.AddScoped<ILoiConformanceService, Application.Services.Loi.LoiConformanceService>();
            services.AddScoped<ILoiCheckService, Application.Services.Loi.LoiCheckService>();
            services.AddSingleton<IFileTextExtractor, FileTextExtractorService>();
            services.AddSingleton<ITextChunker, TextChunkerService>();

            services.AddSingleton<IngestBackgroundService>();
            services.AddSingleton<IIngestBackgroundService>(sp => sp.GetRequiredService<IngestBackgroundService>());
            services.AddSingleton<NameMatchContentBackgroundService>();
            services.AddSingleton<INameMatchContentBackgroundService>(sp => sp.GetRequiredService<NameMatchContentBackgroundService>());

            services.AddMemoryCache();
            services.AddHttpClient();
            services.AddHttpClient<IViewerService, ViewerService>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config["Aps:BaseUrl"] ?? "https://developer.api.autodesk.com";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(10);   // upload file CAD/BIM lớn
            });

            // Convert DWG/DWGX -> PDF de ky so truc quan 
            services.AddHttpClient<ICadToPdfConverter, ConvertApiCadToPdfConverter>((sp, client) =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var baseUrl = config["ConvertApi:BaseUrl"] ?? "https://v2.convertapi.com";
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromMinutes(5);
            });

            return services;
        }
    }
}
