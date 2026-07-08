namespace Domain.Enum.Rag
{
    // Trạng thái nạp 1 tài liệu vào index RAG (phục vụ pipeline + debug).
    public enum DocumentIngestStatus
    {
        Pending = 0,    // đã tạo bản ghi, chưa sinh embedding
        Embedded = 1,   // đã chunk + embed xong, sẵn sàng truy vấn
        Failed = 2      // sinh embedding lỗi (xem log, có thể retry)
    }
}
