using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.Pdf.Parsing;
using System.Text;


namespace Application.Services
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

        private static string ExtractWord(Stream input)
        {
            using var doc = new WordDocument(input, FormatType.Automatic); 
            return doc.GetText();                                          
        }

        private static string ExtractPdf(Stream input)
        {
            using var loaded = new PdfLoadedDocument(input);
            var sb = new StringBuilder();
            for (int i = 0; i < loaded.Pages.Count; i++)
                sb.AppendLine(loaded.Pages[i].ExtractText());  
            return sb.ToString();
        }

        private string Norm(string format)
        {
            if (string.IsNullOrWhiteSpace(format)) return string.Empty;
            return format.Trim().TrimStart('.').ToLowerInvariant();
        }

        public bool CanExtract(string format) => Supported.Contains(Norm(format));
    }
}
