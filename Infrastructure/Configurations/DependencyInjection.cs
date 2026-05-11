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
            services.AddScoped<IAccountService, AccountService>();

            return services;
        }
    }
}
