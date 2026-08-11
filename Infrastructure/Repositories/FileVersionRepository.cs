using Application.Interfaces.IRepositories;
using Domain.Entities;
using Infrastructure.DbContexts;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories
{
    public class FileVersionRepository : GenericRepository<FileVersionState>, IFileVersionRepository
    {
        private readonly CDESystemDbContext _context;

        public FileVersionRepository(CDESystemDbContext context) : base(context)
        {
            _context = context;
        }

        // Tài liệu hiện có = FileItem cùng Name trong cùng folder (theo yêu cầu giai đoạn này).
        public async Task<FileItem?> FindExistingDocumentAsync(Guid folderId, string fileName)
        {
            return await _context.FileItems
                .AsNoTracking()
                .FirstOrDefaultAsync(f => f.FolderId == folderId && f.Name == fileName);
        }

        // Tên file phải là DUY NHẤT trong phạm vi DỰ ÁN, xét theo (Name + đuôi file hiện hành).
        // Trùng tên nhưng KHÁC đuôi (vd Plan.pdf vs Plan.docx) được phép cùng tồn tại.
        // Bỏ qua chính folder đang upload (trùng tên cùng folder = upload thay thế, do
        // FindExistingDocumentAsync xử lý). Đuôi lấy từ Format của version hiện hành (CurrentVersionId).
        public async Task<FileItem?> FindProjectDuplicateByNameAndFormatAsync(Guid folderId, string fileName, string format)
        {
            var projectId = await _context.Folders
                .Where(f => f.Id == folderId)
                .Select(f => (Guid?)f.ProjectId)
                .FirstOrDefaultAsync();
            if (projectId == null)
                return null;

            return await _context.FileItems
                .AsNoTracking()
                .Where(file =>
                    file.FolderId != folderId
                    && file.Name == fileName
                    && file.Folder.ProjectId == projectId.Value
                    && _context.FileVersionStates.Any(v => v.Id == file.CurrentVersionId && v.Format == format))
                .FirstOrDefaultAsync();
        }

        // Không AsNoTracking: service cần entity tracked để set IsCurrent = false rồi SaveChangesAsync.
        public async Task<FileVersionState?> GetCurrentStateAsync(Guid fileItemId)
        {
            return await _context.FileVersionStates
                .FirstOrDefaultAsync(s => s.FileItemId == fileItemId && s.IsCurrent);
        }

        public async Task<List<FileVersionState>> GetHistoryAsync(Guid fileItemId)
        {
            return await _context.FileVersionStates
                .Where(s => s.FileItemId == fileItemId)
                .OrderByDescending(s => s.CreatedAt)
                .AsNoTracking()
                .ToListAsync();
        }

    }
}
