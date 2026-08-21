namespace Domain.Enum.File
{
    // Người dùng muốn gì khi tên tài liệu đã có người chiếm trong thư mục đích.
    // Trước đây hệ thống TỰ lên phiên bản — im lặng đè lên tài liệu của người khác là ca dễ mất
    // dữ liệu nhất, nên giờ client phải nói rõ ý định; không nói thì upload bị từ chối.
    public enum UploadDuplicateAction
    {
        None = 0,        // chưa chọn -> BE trả 409 kèm thông tin để client hỏi người dùng
        NewVersion = 1,  // lên phiên bản mới của chính tài liệu đang có (lịch sử nối tiếp)
        NewDocument = 2  // giữ nguyên tài liệu cũ, tạo tài liệu riêng với tên chưa ai dùng
    }
}
