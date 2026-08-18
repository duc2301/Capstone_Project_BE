namespace Application.Interfaces.IRepositories
{
    public interface IDocumentIndexRepository
    {
        /// <summary>
        /// Id của các tệp đang ở vùng chính thức nhưng phiên bản hiện hành chưa có Document đã embed.
        /// Lọc hoàn toàn trong DB (một truy vấn) — danh sách này có thể quét toàn hệ thống.
        /// </summary>
        Task<IReadOnlyList<Guid>> GetUnindexedFileItemIdsAsync(CancellationToken ct = default);
    }
}
