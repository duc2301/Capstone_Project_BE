namespace Application.Interfaces.IServices
{
    // Hàng đợi (in-memory) các FileVersion cần dịch lên APS, do ModelTranslationWorker tiêu thụ ở nền.
    // Đăng ký SINGLETON: cả producer (upload/view) lẫn consumer (worker) dùng chung một instance.
    public interface IModelTranslationQueue
    {
        // Đẩy 1 FileVersion vào hàng đợi. An toàn gọi nhiều lần (worker tự bỏ qua bản đã Ready).
        void Enqueue(Guid fileVersionId);

        // Worker đọc tuần tự các id tới khi bị huỷ (host dừng).
        IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken ct);
    }
}
