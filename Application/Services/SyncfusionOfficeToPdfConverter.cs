using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using Syncfusion.Pdf;
using Syncfusion.Presentation;
using Syncfusion.PresentationRenderer;
using Syncfusion.XlsIO;
using Syncfusion.XlsIORenderer;

namespace Application.Services
{
    // Convert Office -> PDF bằng Syncfusion (Community license đăng ký ở Program.cs).
    public class SyncfusionOfficeToPdfConverter : IOfficeToPdfConverter
    {
        private enum DocKind { Word, Excel, Ppt }

        private static readonly string[] WordExts = { ".doc", ".docx" };
        private static readonly string[] ExcelExts = { ".xls", ".xlsx" };
        private static readonly string[] PptExts = { ".ppt", ".pptx" };

        public bool CanConvert(string extension) => Category(Norm(extension)) is not null;

        public Task<Stream> ConvertToPdfAsync(Stream content, string extension, CancellationToken ct = default)
        {
            var ext = Norm(extension);
            var kind = Category(ext)
                ?? throw new ApiExceptionResponse($"Không hỗ trợ convert '{ext}' sang PDF.", 400);

            // API Syncfusion là đồng bộ + nặng -> đẩy sang threadpool để không chặn lâu thread xử lý request.
            return Task.Run<Stream>(() =>
            {
                ct.ThrowIfCancellationRequested();

                // Syncfusion cần stream SEEKABLE (đọc Position để dò định dạng). Stream từ S3 (HashStream)
                // không seek được -> buffer vào RAM trước. File Office thường nhỏ nên chấp nhận được.
                Stream input = content;
                MemoryStream? buffered = null;
                if (!content.CanSeek)
                {
                    buffered = new MemoryStream();
                    content.CopyTo(buffered);
                    buffered.Position = 0;
                    input = buffered;
                }

                try
                {
                    var output = new MemoryStream();
                    switch (kind)
                    {
                        case DocKind.Word: WordToPdf(input, output); break;
                        case DocKind.Excel: ExcelToPdf(input, output); break;
                        case DocKind.Ppt: PptToPdf(input, output); break;
                    }
                    output.Position = 0;
                    return output;
                }
                finally
                {
                    buffered?.Dispose();
                }
            }, ct);
        }

        private static void WordToPdf(Stream input, Stream output)
        {
            using var doc = new WordDocument(input, Syncfusion.DocIO.FormatType.Automatic);
            using var renderer = new DocIORenderer();
            using PdfDocument pdf = renderer.ConvertToPDF(doc);
            pdf.Save(output);
        }

        private static void ExcelToPdf(Stream input, Stream output)
        {
            using var engine = new ExcelEngine();
            var app = engine.Excel;
            app.DefaultVersion = ExcelVersion.Xlsx;
            var workbook = app.Workbooks.Open(input);

            var renderer = new XlsIORenderer();
            var settings = new XlsIORendererSettings
            {
                // Co toàn bộ cột vừa 1 trang theo chiều ngang -> không tràn cột sang trang phụ.
                LayoutOptions = LayoutOptions.FitAllColumnsOnOnePage
            };
            using PdfDocument pdf = renderer.ConvertToPDF(workbook, settings);
            pdf.Save(output);
        }

        private static void PptToPdf(Stream input, Stream output)
        {
            using IPresentation presentation = Presentation.Open(input);
            using PdfDocument pdf = PresentationToPdfConverter.Convert(presentation);
            pdf.Save(output);
        }

        private static DocKind? Category(string ext) =>
            WordExts.Contains(ext) ? DocKind.Word
            : ExcelExts.Contains(ext) ? DocKind.Excel
            : PptExts.Contains(ext) ? DocKind.Ppt
            : null;

        private static string Norm(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            ext = ext.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }
    }
}
