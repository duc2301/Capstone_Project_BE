namespace Application.Interfaces.IServices
{
    /// <summary>
    /// Điểm vào duy nhất để giữ chỉ mục ngữ nghĩa khớp với trạng thái thật của cây thư mục.
    /// Service nghiệp vụ chỉ báo "tệp này vừa đổi chỗ / đổi nội dung", KHÔNG tự nhớ vùng nào được
    /// index — nếu để mỗi luồng tự nhớ gọi Enqueue thì cứ thêm một đường tới Published là thêm
    /// một lần quên (đã xảy ra với luồng upload thẳng vào thư mục hệ thống).
    /// </summary>
    public interface IDocumentIndexSyncService
    {
        /// <summary>
        /// Xin index một tệp. Tự bỏ qua nếu tệp không nằm ở vùng chính thức hoặc chưa có nội dung.
        /// Gọi SAU khi đã CommitAsync để worker đọc được version vừa ghi.
        /// </summary>
        Task RequestIndexAsync(Guid fileItemId, CancellationToken ct = default);

        /// <summary>
        /// Quét đối soát: tìm mọi tệp đang ở vùng chính thức mà phiên bản hiện hành chưa có vector
        /// rồi đẩy vào hàng đợi. Trả về số tệp đã đẩy. Đây là lưới an toàn cho những đường ghi
        /// quên gọi RequestIndexAsync, đồng thời là cách backfill dữ liệu cũ.
        /// </summary>
        Task<int> SyncPendingAsync(CancellationToken ct = default);
    }
}
