using Infrastructure.DbContexts;
using Infrastructure.Seed;

namespace Capstone_Project.Extensions
{
    public static class MigrationsExtension
    {
        public static IHost ApplyMigrations(this IHost host)
        {
            host.MigrateDatabase<CDESystemDbContext>();

            using (var scope = host.Services.CreateScope())
            {
                var services = scope.ServiceProvider;
                var logger = services.GetRequiredService<ILogger<CDESystemDbContext>>();
                try
                {
                    var db = services.GetRequiredService<CDESystemDbContext>();
                    LoiSeeder.SeedAsync(db, logger).GetAwaiter().GetResult();
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Failed to seed LOI rules.");
                }
            }

            return host;
        }
    }
}
