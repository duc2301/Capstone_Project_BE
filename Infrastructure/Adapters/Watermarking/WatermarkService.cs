using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Services;
using Domain.Entities;
using Domain.Enum.Cde;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.CustomProperties;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.VariantTypes;
using Infrastructure.Adapters;
using iText.IO.Font;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using iText.Kernel.Pdf.Extgstate;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.Extensions.Logging;
using SkiaSharp;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Infrastructure.Adapters.Watermarking
{
    // Đóng watermark định danh người xem/tải: PDF qua itext7, Word/Excel qua Open XML SDK (+ SkiaSharp
    // để vẽ ảnh nền cho Excel, vì Excel không có watermark có sẵn).
    public class WatermarkService : IWatermarkService
    {
        private const string BrandText = "CDE PORTAL";

        // Đánh dấu ẩn để biết "đã watermark" — tránh chồng lớp nếu file bị tải-rồi-upload-lại.
        private const string WatermarkMarkerKey = "CdeWatermarked";

        private readonly IUnitOfWork _unitOfWork;
        private readonly ILogger<WatermarkService> _logger;

        public WatermarkService(IUnitOfWork unitOfWork, ILogger<WatermarkService> logger)
        {
            _unitOfWork = unitOfWork;
            _logger = logger;
        }

        public async Task<Stream> ApplyAsync(Stream input, string format, CdeArea? area, Guid actorId, CancellationToken ct = default)
        {
            if (area is not (CdeArea.Shared or CdeArea.Published))
                return input;

            var account = await _unitOfWork.Repository<Account>().GetByIdAsync(actorId);
            if (account == null)
            {
                _logger.LogWarning("Khong tim thay Account {ActorId} de dong watermark, bo qua.", actorId);
                return input;
            }

            return Stamp(input, format, WatermarkLabelBuilder.Build(account));
        }

        public Stream Stamp(Stream input, string format, string label)
        {
            var normalized = format.TrimStart('.').ToLowerInvariant();
            return normalized switch
            {
                "pdf" => StampPdf(input, label),
                "docx" => StampWord(input, label),
                "xlsx" => StampExcel(input, label),
                _ => input
            };
        }

        // ================= PDF (itext7) =================

        private const float PdfFontSize = 12f;
        private const float PdfFillOpacity = 0.12f;
        private const float PdfRotationRadians = (float)(Math.PI / 6); // ~30 độ

        private static readonly Lazy<byte[]> _pdfFontBytes = new(() => EmbeddedResourceLoader.LoadFontBytes("NotoSans-Regular.ttf"));

        private static Stream StampPdf(Stream pdfInput, string label)
        {
            using var inputBuffer = new MemoryStream();
            pdfInput.CopyTo(inputBuffer);
            var inputBytes = inputBuffer.ToArray();

            if (IsAlreadyWatermarkedPdf(inputBytes))
                return new MemoryStream(inputBytes);

            var outputStream = new MemoryStream();
            var reader = new PdfReader(new MemoryStream(inputBytes));
            var writer = new PdfWriter(outputStream);
            writer.SetCloseStream(false); // MemoryStream còn cần đọc lại sau khi PdfDocument.Close()

            var font = PdfFontFactory.CreateFont(_pdfFontBytes.Value, PdfEncodings.IDENTITY_H);
            var extGState = new PdfExtGState().SetFillOpacity(PdfFillOpacity);

            using (var pdfDocument = new PdfDocument(reader, writer))
            {
                pdfDocument.GetDocumentInfo().SetMoreInfo(WatermarkMarkerKey, "1");

                var pageCount = pdfDocument.GetNumberOfPages();
                for (var i = 1; i <= pageCount; i++)
                    StampPdfGrid(pdfDocument.GetPage(i), i, font, extGState, label);
            }

            outputStream.Position = 0;
            return outputStream;
        }

        private static bool IsAlreadyWatermarkedPdf(byte[] pdfBytes)
        {
            var reader = new PdfReader(new MemoryStream(pdfBytes));
            using var pdfDocument = new PdfDocument(reader);
            return pdfDocument.GetDocumentInfo().GetMoreInfo(WatermarkMarkerKey) == "1";
        }

        // Lặp lưới nhiều vị trí (không chỉ 1 chỗ) để khó bị crop/che mất khi chụp 1 phần màn hình.
        private static void StampPdfGrid(PdfPage page, int pageNumber, PdfFont font, PdfExtGState extGState, string label)
        {
            var size = page.GetPageSize();
            var pdfCanvas = new PdfCanvas(page);
            pdfCanvas.SaveState();
            pdfCanvas.SetExtGState(extGState);
            pdfCanvas.SetFillColor(ColorConstants.GRAY);

            var paragraph = new Paragraph($"{BrandText}\n{label}")
                .SetFont(font).SetFontSize(PdfFontSize).SetFontColor(ColorConstants.GRAY)
                .SetTextAlignment(TextAlignment.CENTER)
                .SetMultipliedLeading(1.2f);

            const int columns = 2;
            const int rows = 4;
            using var layoutCanvas = new Canvas(pdfCanvas, size);
            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < columns; col++)
                {
                    var x = size.GetWidth() * (col + 0.5f) / columns;
                    var y = size.GetHeight() * (row + 0.5f) / rows;
                    layoutCanvas.ShowTextAligned(
                        paragraph, x, y, pageNumber,
                        TextAlignment.CENTER, VerticalAlignment.MIDDLE, PdfRotationRadians);
                }
            }

            pdfCanvas.RestoreState();
        }

        // ================= Word (Open XML SDK) =================

        private static Stream StampWord(Stream docxInput, string label)
        {
            var buffer = new MemoryStream();
            docxInput.CopyTo(buffer);
            var originalBytes = buffer.ToArray();

            // Kiểm tra trước ở chế độ chỉ-đọc để tránh mở editable không cần thiết (mở rồi đóng dù
            // không sửa gì vẫn khiến zip bị nén lại khác byte).
            buffer.Position = 0;
            using (var probe = WordprocessingDocument.Open(buffer, false))
            {
                if (IsAlreadyWatermarkedOffice(probe.CustomFilePropertiesPart))
                    return new MemoryStream(originalBytes);
            }

            buffer.Position = 0;
            using (var doc = WordprocessingDocument.Open(buffer, true))
            {
                var mainPart = doc.MainDocumentPart;
                var body = mainPart?.Document?.Body;
                if (mainPart != null && body != null)
                {
                    var watermarkParagraph = BuildWatermarkParagraph(label);

                    // Nếu section đã có header (vd letterhead công ty) thì CHÈN THÊM watermark vào
                    // header đó, không thay thế — Word chỉ cho 1 header mặc định/section nên không thể
                    // "chồng lớp" bằng cách tạo header thứ 2, phải chèn nội dung vào header đã có.
                    var handledHeaderIds = new HashSet<string>();
                    foreach (var sectPr in body.Descendants<W.SectionProperties>())
                    {
                        var existingRef = sectPr.Elements<W.HeaderReference>()
                            .FirstOrDefault(r => r.Type is null || r.Type == W.HeaderFooterValues.Default);

                        if (existingRef?.Id?.Value is string existingId)
                        {
                            if (handledHeaderIds.Add(existingId)
                                && mainPart.GetPartById(existingId) is HeaderPart existingHeaderPart)
                            {
                                existingHeaderPart.Header.AppendChild((W.Paragraph)watermarkParagraph.CloneNode(true));
                                existingHeaderPart.Header.Save();
                            }
                        }
                        else
                        {
                            var headerPart = mainPart.AddNewPart<HeaderPart>();
                            var headerPartId = mainPart.GetIdOfPart(headerPart);
                            headerPart.Header = new W.Header((W.Paragraph)watermarkParagraph.CloneNode(true));
                            headerPart.Header.Save();

                            sectPr.PrependChild(new W.HeaderReference
                            {
                                Type = W.HeaderFooterValues.Default,
                                Id = headerPartId
                            });
                        }
                    }

                    SetOfficeMarker(doc.CustomFilePropertiesPart, doc.AddCustomFilePropertiesPart);
                    mainPart.Document.Save();
                }
            }

            buffer.Position = 0;
            return buffer;
        }

        // Trả về đúng 1 đoạn <w:p> chứa watermark (VML shape xoay, giống Word tự tạo khi bấm "Insert
        // Watermark") — dùng CloneNode để chèn vào header có sẵn hoặc header mới tạo.
        private static W.Paragraph BuildWatermarkParagraph(string label)
        {
            var text = XmlEscape($"{BrandText} · {label}");
            var xml = $@"<w:p xmlns:w=""http://schemas.openxmlformats.org/wordprocessingml/2006/main""
                    xmlns:v=""urn:schemas-microsoft-com:vml""
                    xmlns:o=""urn:schemas-microsoft-com:office:office"">
    <w:pPr><w:pStyle w:val=""Header""/></w:pPr>
    <w:r>
      <w:rPr><w:noProof/></w:rPr>
      <w:pict>
        <v:shapetype id=""_x0000_t136"" coordsize=""1600,21600"" o:spt=""136"" adj=""10800""
            path=""m@7,0l@8,5400,,10800,,10800,,10800@8,16200@7,21600,@9,21600,@10,16200,,10800,,10800,,10800@10,5400@9,0xe"">
          <v:formulas>
            <v:f eqn=""sum #0 0 10800""/>
            <v:f eqn=""prod #0 2 1""/>
            <v:f eqn=""prod #0 4 1""/>
            <v:f eqn=""prod #0 4 3""/>
            <v:f eqn=""sum @0 @1 0""/>
            <v:f eqn=""sum @2 @2 0""/>
            <v:f eqn=""sum #1 @3 0""/>
            <v:f eqn=""sum 21600 0 @6""/>
            <v:f eqn=""sum @1 0 @5""/>
          </v:formulas>
          <v:path textpathok=""t"" o:connecttype=""custom""/>
          <v:textpath on=""t"" fitshape=""t""/>
          <v:handles>
            <v:h position=""#0,bottomRight"" xrange=""6629,14971""/>
            <v:h position=""#1,bottomRight"" xrange=""0,21600""/>
          </v:handles>
        </v:shapetype>
        <v:shape id=""CdeWaterMarkObject"" o:spid=""_x0000_s1026"" type=""#_x0000_t136""
            style=""position:absolute;margin-left:0;margin-top:0;width:415pt;height:207.5pt;
            rotation:315;z-index:-251658752;mso-position-horizontal:center;
            mso-position-horizontal-relative:margin;mso-position-vertical:center;
            mso-position-vertical-relative:margin"" o:allowincell=""f"" fillcolor=""#D9D9D9"" stroked=""f"">
          <v:fill opacity="".5""/>
          <v:textpath style=""font-family:'Calibri';font-size:1pt"" string=""{text}""/>
        </v:shape>
      </w:pict>
    </w:r>
  </w:p>";
            return new W.Paragraph(xml);
        }

        // ================= Excel (Open XML SDK + SkiaSharp) =================

        private static Stream StampExcel(Stream xlsxInput, string label)
        {
            var buffer = new MemoryStream();
            xlsxInput.CopyTo(buffer);
            var originalBytes = buffer.ToArray();

            buffer.Position = 0;
            using (var probe = SpreadsheetDocument.Open(buffer, false))
            {
                if (IsAlreadyWatermarkedOffice(probe.CustomFilePropertiesPart))
                    return new MemoryStream(originalBytes);
            }

            buffer.Position = 0;
            using (var doc = SpreadsheetDocument.Open(buffer, true))
            {
                var workbookPart = doc.WorkbookPart;
                if (workbookPart != null)
                {
                    var imageBytes = RenderExcelWatermarkImage(label);

                    foreach (var worksheetPart in workbookPart.WorksheetParts)
                    {
                        var imagePart = worksheetPart.AddImagePart(ImagePartType.Png);
                        using (var imageStream = new MemoryStream(imageBytes))
                            imagePart.FeedData(imageStream);

                        worksheetPart.Worksheet.RemoveAllChildren<DocumentFormat.OpenXml.Spreadsheet.Picture>();
                        InsertWorksheetPicture(worksheetPart.Worksheet, new DocumentFormat.OpenXml.Spreadsheet.Picture
                        {
                            Id = worksheetPart.GetIdOfPart(imagePart)
                        });
                        worksheetPart.Worksheet.Save();
                    }

                    SetOfficeMarker(doc.CustomFilePropertiesPart, doc.AddCustomFilePropertiesPart);
                }
            }

            buffer.Position = 0;
            return buffer;
        }

        // CT_Worksheet có thứ tự phần tử con cố định — <picture> phải nằm TRƯỚC oleObjects/controls/
        // webPublishItems/tableParts/extLst nếu sheet có các phần tử đó (vd sheet có chèn Bảng Excel),
        // không thể Append thẳng ra cuối như bình thường vì sẽ làm sai schema, Excel báo lỗi khi mở.
        private static void InsertWorksheetPicture(Worksheet worksheet, DocumentFormat.OpenXml.Spreadsheet.Picture picture)
        {
            var followingSibling = worksheet.Elements().FirstOrDefault(e =>
                e is OleObjects or Controls or WebPublishItems or TableParts or WorksheetExtensionList);

            if (followingSibling != null)
                worksheet.InsertBefore(picture, followingSibling);
            else
                worksheet.AppendChild(picture);
        }

        private static byte[] RenderExcelWatermarkImage(string label)
        {
            const int width = 1600;
            const int height = 1000;
            using var bitmap = new SKBitmap(width, height);
            using (var canvas = new SKCanvas(bitmap))
            {
                canvas.Clear(SKColors.Transparent);
                using var font = new SKFont(SKTypeface.Default, 30);
                using var paint = new SKPaint { Color = new SKColor(150, 150, 150, 60), IsAntialias = true };

                canvas.Save();
                canvas.Translate(width / 2f, height / 2f);
                canvas.RotateDegrees(-30);
                canvas.DrawText(BrandText, 0, -20, SKTextAlign.Center, font, paint);
                canvas.DrawText(label, 0, 20, SKTextAlign.Center, font, paint);
                canvas.Restore();
            }

            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        // ================= Word/Excel: đánh dấu chung (Custom Property) =================

        private static bool IsAlreadyWatermarkedOffice(CustomFilePropertiesPart? part)
            => part?.Properties?.Elements<CustomDocumentProperty>()
                .Any(p => p.Name == WatermarkMarkerKey) == true;

        // Dùng part Custom Properties SẴN CÓ nếu file đã có (vd nhãn "Classification" do DMS khác gắn) —
        // gọi AddCustomFilePropertiesPart() khi đã có 1 part rồi sẽ ném lỗi (chỉ cho phép 1 part/loại).
        // Nhận "existing" + "addPart" riêng vì WordprocessingDocument/SpreadsheetDocument không chung
        // base type có 2 thành viên này.
        private static void SetOfficeMarker(CustomFilePropertiesPart? existing, Func<CustomFilePropertiesPart> addPart)
        {
            var part = existing ?? addPart();
            part.Properties ??= new Properties();

            // PropertyId phải là số duy nhất (>=2) trong toàn part — không được đóng cứng "2" vì có
            // thể trùng với property khác đã tồn tại từ trước.
            var nextId = part.Properties.Elements<CustomDocumentProperty>()
                .Select(p => p.PropertyId?.Value ?? 1)
                .DefaultIfEmpty(1)
                .Max() + 1;

            var property = new CustomDocumentProperty
            {
                FormatId = "{D5CDD505-2E9C-101B-9397-08002B2CF9AE}",
                PropertyId = nextId,
                Name = WatermarkMarkerKey
            };
            property.AppendChild(new VTLPWSTR { Text = "1" });
            part.Properties.AppendChild(property);
            part.Properties.Save();
        }

        private static string XmlEscape(string value)
            => value.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;")
                .Replace("\"", "&quot;").Replace("'", "&apos;");
    }
}
