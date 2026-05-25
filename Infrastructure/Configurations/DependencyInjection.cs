using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Mapping;
using Application.Services;
using Infrastructure.DbContexts;
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

            services.AddScoped<IUnitOfWork, Infrastructure.UnitOfWork.UnitOfWork>();

            // CRUD generic cho mọi aggregate root — đăng ký 1 lần (open generic)
            services.AddScoped(typeof(IGenericService<,,,>), typeof(GenericService<,,,>));

            services.AddScoped<IAccountService, AccountService>();

            // Auth (giống ChemXLab) + refresh token
            services.AddScoped<IJwtService, JwtService>();
            services.AddScoped<IAuthService, AuthService>();

            // Notification dispatcher (event -> tạo Notification rows)
            services.AddScoped<INotificationService, NotificationService>();

            // HttpContextAccessor + ICurrentUserService (đọc AccountId / system role / department roles từ JWT)
            services.AddHttpContextAccessor();
            services.AddScoped<ICurrentUserService, CurrentUserService>();

            // Invitation flow: Manager mời account vô dự án -> accept tạo ProjectParticipant
            services.AddScoped<IInvitationService, InvitationService>();

            // Project flow (custom, ngoài CRUD generic): Admin tạo PM cho project, PM add bên tham gia
            services.AddScoped<IProjectFlowService, ProjectFlowService>();

            return services;
        }
    }
}
