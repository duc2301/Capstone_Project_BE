using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class NamingConventionRepository : GenericRepository<NamingConvention>, INamingConventionRepository
    {
        private readonly CDESystemDbContext _context;

        public NamingConventionRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        private IQueryable<NamingConvention> DetailsQuery(bool track)
        {
            var query = _context.NamingConventions
                .Include(nc => nc.Fields)
                    .ThenInclude(f => f.AllowedValues)
                .Include(nc => nc.Fields)
                    .ThenInclude(f => f.LockedValue)
                        .ThenInclude(lv => lv!.Value)
                .AsQueryable();

            return track ? query : query.AsNoTracking();
        }

        public async Task<NamingConvention?> GetWithDetailsAsync(Guid id, bool track = false)
        {
            return await DetailsQuery(track).FirstOrDefaultAsync(nc => nc.Id == id);
        }

        public async Task<NamingConvention?> GetByFolderIdAsync(Guid folderId)
        {
            var conventionId = await _context.Folders
                .Where(f => f.Id == folderId)
                .Select(f => f.NamingConventionId)
                .FirstOrDefaultAsync();

            if (conventionId == null)
                return null;

            return await DetailsQuery(track: false)
                .FirstOrDefaultAsync(nc => nc.Id == conventionId.Value);
        }

        public async Task<IEnumerable<NamingConvention>> GetByProjectIdAsync(Guid projectId)
        {
            return await DetailsQuery(track: false)
                .Where(nc => nc.ProjectId == projectId)
                .OrderBy(nc => nc.CreatedAt)
                .ToListAsync();
        }

        public async Task<NamingConventionField?> GetFieldWithDetailsAsync(Guid fieldId, bool track = false)
        {
            var query = _context.NamingConventionFields
                .Include(f => f.AllowedValues)
                .Include(f => f.LockedValue)
                    .ThenInclude(lv => lv!.Value)
                .AsQueryable();

            if (!track) query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(f => f.Id == fieldId);
        }

        public async Task<NamingConventionFieldValue?> GetFieldValueAsync(Guid valueId, bool track = false)
        {
            var query = _context.NamingConventionFieldValues
                .Include(v => v.LockedValue)
                .AsQueryable();

            if (!track) query = query.AsNoTracking();

            return await query.FirstOrDefaultAsync(v => v.Id == valueId);
        }

        public async Task<IEnumerable<Folder>> GetAssignedFoldersAsync(Guid conventionId)
        {
            return await _context.Folders
                .Where(f => f.NamingConventionId == conventionId)
                .AsNoTracking()
                .ToListAsync();
        }

        public async Task<List<Folder>> GetFoldersByIdsAsync(IEnumerable<Guid> folderIds, bool track = false)
        {
            var ids = folderIds.ToList();
            var query = _context.Folders.Where(f => ids.Contains(f.Id));
            if (!track) query = query.AsNoTracking();
            return await query.ToListAsync();
        }

        public async Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, bool track = false)
        {
            var query = _context.Folders.Where(f => f.ProjectId == projectId);
            if (!track) query = query.AsNoTracking();
            return await query.ToListAsync();
        }
    }
}
