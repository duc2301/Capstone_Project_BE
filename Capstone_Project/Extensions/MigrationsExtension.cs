using Infrastructure.DbContexts;

namespace Capstone_Project.Extensions
{
    public static class MigrationsExtension
    {
        public static IHost ApplyMigrations(this IHost host)
        {
            host.MigrateDatabase<CDESystemDbContext>();

            return host;
        }
    }
}
