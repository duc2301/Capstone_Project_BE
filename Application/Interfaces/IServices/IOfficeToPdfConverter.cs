namespace Application.Interfaces.IServices
{
    // Convert nội dung file Office (Word/Excel/PowerPoint) sang PDF để xem inline trên web.
    public interface IOfficeToPdfConverter
    {
        // true nếu đuôi file thuộc loại convert được (doc/docx/xls/xlsx/ppt/pptx).
        bool CanConvert(string extension);

        // Convert -> PDF. Trả MemoryStream đã seek về 0. Ném ApiExceptionResponse nếu không hỗ trợ.
        Task<Stream> ConvertToPdfAsync(Stream content, string extension, CancellationToken ct = default);
    }
}
