using Domain.Entities;

namespace Application.Interfaces.IRepositories
{
    // Truy vấn dữ liệu naming convention (fields + allowed values + locked values + folder gán).
    // Không chứa business logic — validate/sinh tên nằm ở NamingConventionService.
    public interface INamingConventionRepository : IGenericRepository<NamingConvention>
    {
        // track = true: entity được ChangeTracker theo dõi để mutate rồi SaveChanges.
        Task<NamingConvention?> GetWithDetailsAsync(Guid id, bool track = false);

        // Convention đang gán cho folder (qua Folder.NamingConventionId), kèm đầy đủ chi tiết.
        Task<NamingConvention?> GetByFolderIdAsync(Guid folderId);

        Task<IEnumerable<NamingConvention>> GetByProjectIdAsync(Guid projectId);

        Task<NamingConventionField?> GetFieldWithDetailsAsync(Guid fieldId, bool track = false);

        Task<NamingConventionFieldValue?> GetFieldValueAsync(Guid valueId, bool track = false);

        Task<IEnumerable<Folder>> GetAssignedFoldersAsync(Guid conventionId);

        // Folder theo danh sách id, kèm toàn bộ folder của cùng project để duyệt cây con.
        Task<List<Folder>> GetFoldersByIdsAsync(IEnumerable<Guid> folderIds, bool track = false);

        Task<List<Folder>> GetProjectFoldersAsync(Guid projectId, bool track = false);
    }
}
