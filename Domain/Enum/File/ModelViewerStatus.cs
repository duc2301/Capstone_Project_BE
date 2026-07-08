namespace Domain.Enum.File
{
    /// <summary>
    /// Trạng thái dịch model (IFC/CAD) lên Autodesk APS của 1 <c>FileVersion</c>.
    /// Dịch chạy ở hàng đợi nền (xem ModelTranslationWorker) nên không gắn với request trình duyệt.
    /// </summary>
    public enum ModelViewerStatus
    {
        /// <summary>Chưa xử lý (file không phải model, hoặc file cũ tải lên trước khi có cơ chế dịch nền).</summary>
        None = 0,

        /// <summary>Đã đưa vào hàng đợi, đang chờ worker xử lý.</summary>
        Pending = 1,

        /// <summary>Worker đang đẩy file lên APS và/hoặc đang dịch.</summary>
        Processing = 2,

        /// <summary>Đã dịch xong, ViewerUrn sẵn sàng để mở viewer.</summary>
        Ready = 3,

        /// <summary>Dịch thất bại (file hỏng/APS lỗi); cho phép dịch lại.</summary>
        Failed = 4
    }
}
