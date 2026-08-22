namespace Application.Interfaces.IServices
{
    // Tệp do hệ thống sinh ra từ một tệp gốc (không phải bản người dùng tải lên).
    public enum DerivedFileKind
    {
        Preview,    // bản PDF cache để xem trực tuyến (Office/CAD -> PDF)
        Signed,     // bản PDF đã đóng dấu chữ ký số
        Prepared    // bản PDF đã chèn chỗ trống chữ ký, chờ ký ngoài (SmartCA)
    }

    /// <summary>
    /// Nơi DUY NHẤT quyết định bố cục key trên kho lưu trữ:
    /// <c>projects/{ten-du-an}--{id8}/{duong-dan-thu-muc}/{TenTheoNaming}_{Version}__{id8}.{ext}</c>
    ///
    /// Key sinh ra là BẤT BIẾN. Đổi tên dự án/thư mục hay chuyển vùng CDE đều KHÔNG ghi lại key,
    /// vì một object đang được nhiều dòng FileVersionState dùng chung (xem
    /// <c>FileVersionService.CopyContentFrom</c>) — dời nó sẽ làm hỏng StoragePath của các dòng lịch sử.
    ///
    /// Hệ quả bắt buộc nhớ: KHÔNG BAO GIỜ dựng lại key từ entity để đọc/xoá tệp. Mọi thao tác đọc
    /// phải lấy StoragePath đã lưu trong DB — đó là điều kiện để key cũ và key mới cùng sống trên
    /// một bucket sau khi đổi bố cục.
    /// </summary>
    public interface ICdeStorageKeyBuilder
    {
        // Bản người dùng tải lên. displayVersion chỉ để ĐẶT TÊN cho dễ đọc; số version thật nằm ở DB.
        Task<StorageObjectName> ForDocumentAsync(
            Guid folderId, string fileNameWithoutExtension, string? displayVersion, string extension,
            CancellationToken ct = default);

        // Tệp phái sinh: để riêng dưới _derived/ cho khỏi lẫn vào cây tài liệu gốc.
        Task<StorageObjectName> ForDerivedAsync(
            Guid folderId, DerivedFileKind kind, string extension, CancellationToken ct = default);

        // Đính kèm của Issue: để riêng dưới _issues/{issueId}/.
        Task<StorageObjectName> ForIssueAttachmentAsync(
            Guid folderId, Guid issueId, string originalFileName, CancellationToken ct = default);
    }
}
