using Application.Services.Loi;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Seed
{
    public static class LoiSeeder
    {
        public static async Task SeedAsync(CDESystemDbContext db, ILogger logger, CancellationToken ct = default)
        {
            if (await db.LoiRequirements.AnyAsync(ct))
                return;

            var (requirements, aliases) = LoiSeedData.Build();
            await db.LoiRequirements.AddRangeAsync(requirements, ct);
            await db.LoiFieldAliases.AddRangeAsync(aliases, ct);
            await db.SaveChangesAsync(ct);

            logger.LogInformation("Seeded LOI rules: {Requirements} requirements, {Aliases} aliases.",
                requirements.Count, aliases.Count);
        }
    }
}
