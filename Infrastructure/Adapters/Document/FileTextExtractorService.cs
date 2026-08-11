using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.Pdf;
using Syncfusion.Pdf.Parsing;
using System.Text;


namespace Infrastructure.Adapters.Document
{
    public class FileTextExtractorService : IFileTextExtractor
    {
        private static readonly HashSet<string> Supported = new HashSet<string> { "pdf", "docx", "doc", "txt", "md" };

        public async Task<string> ExtractTextAsync(Stream Content, string format, CancellationToken cancellationToken = default)
        {
            var fmt  = Norm(format);
            if (!CanExtract(fmt)) throw new ApiExceptionResponse($"Unsupported file format: {format}");

            return await Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (fmt is "txt" or "md")
                {
                    return new StreamReader(Content).ReadToEnd();                    
                }

                Stream input = Content;
                MemoryStream? buffered = null;
                if(!Content.CanSeek)
                {
                    buffered = new MemoryStream();
                    Content.CopyTo(buffered);
                    buffered.Position = 0;
                    input = buffered;
                }

                try
                {
                    return fmt == "pdf" ? ExtractPdf(input) : ExtractWord(input);
                }
                catch (Exception ex)
                {

                    throw new ApiExceptionResponse($"Error extracting text from {format}: {ex.Message}", 400);
                }
                finally
                {
                    buffered?.Dispose();
                }
                
            }, cancellationToken);
        }

        // GetText() nối phẳng toàn bộ tài liệu, làm mất ranh giới ô và hàng của bảng —
        // mà thông tin các bên tham gia trong BEP nằm chủ yếu ở bảng phân công/phân quyền.
        // Duyệt tay để giữ cấu trúc: mỗi hàng một dòng, các ô ngăn bằng " | ".
        private static string ExtractWord(Stream input)
        {
            using var doc = new WordDocument(input, FormatType.Automatic);
            var sb = new StringBuilder();
            foreach (WSection section in doc.Sections)
                AppendBody(section.Body, sb);
            return sb.ToString();
        }

        private static void AppendBody(WTextBody body, StringBuilder sb)
        {
            foreach (var item in body.ChildEntities)
            {
                switch (item)
                {
                    case WParagraph paragraph:
                        sb.AppendLine(paragraph.Text);
                        break;

                    case WTable table:
                        foreach (WTableRow row in table.Rows)
                        {
                            var cells = new List<string>(row.Cells.Count);
                            foreach (WTableCell cell in row.Cells)
                            {
                                var cellText = new StringBuilder();
                                AppendBody(cell, cellText);
                                cells.Add(cellText.ToString().Replace('\r', ' ').Replace('\n', ' ').Trim());
                            }
                            sb.AppendLine(string.Join(" | ", cells));
                        }
                        sb.AppendLine();
                        break;
                }
            }
        }

        // Khoảng trống ngang tối thiểu (theo tỉ lệ chiều cao chữ) để coi là có dấu cách.
        private const float SpaceGapRatio = 0.22f;

        private static string ExtractPdf(Stream input)
        {
            using var loaded = new PdfLoadedDocument(input);
            var sb = new StringBuilder();
            for (int i = 0; i < loaded.Pages.Count; i++)
                AppendPageText(loaded.Pages[i], sb);
            return sb.ToString();
        }


        private static void AppendPageText(PdfPageBase page, StringBuilder sb)
        {
            var flat = page.ExtractText(out TextLineCollection lines);

            // PDF ảnh/scan hoặc trang không lấy được hình học -> quay về bản phẳng giữ bố cục.
            if (lines?.TextLine is null || lines.TextLine.Count == 0)
            {
                sb.AppendLine(string.IsNullOrEmpty(flat) ? page.ExtractText(true) : flat);
                return;
            }

            foreach (var line in lines.TextLine)
            {
                var lineText = new StringBuilder();
                float previousRight = float.NaN;

                foreach (var word in line.WordCollection)
                {
                    var text = word.Text;
                    if (string.IsNullOrEmpty(text)) continue;

                    if (!float.IsNaN(previousRight)
                        && word.Bounds.Left - previousRight > word.Bounds.Height * SpaceGapRatio)
                        lineText.Append(' ');

                    lineText.Append(text);
                    previousRight = word.Bounds.Right;
                }

                sb.AppendLine(lineText.ToString());
            }
            sb.AppendLine();
        }

        private string Norm(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return string.Empty;
            return format.Trim().TrimStart('.').ToLowerInvariant();
        }

        public bool CanExtract(string format) => Supported.Contains(Norm(format));
    }
}
