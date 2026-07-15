using System.Net.Http.Headers;
using System.Text.Json;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    // Convert DWG/DWGX -> PDF qua ConvertAPI (https://www.convertapi.com)
    public class ConvertApiCadToPdfConverter : ICadToPdfConverter
    {
        private static readonly string[] CadExts = { ".dwg", ".dwgx" };

        private readonly HttpClient _http;
        private readonly string _apiToken;

        public ConvertApiCadToPdfConverter(HttpClient http, IConfiguration config)
        {
            _http = http;
            _apiToken = config["ConvertApi:ApiToken"] ?? string.Empty;
        }

        public bool CanConvert(string extension) => CadExts.Contains(Norm(extension));

        public async Task<Stream> ConvertToPdfAsync(Stream content, string extension, CancellationToken ct = default)
        {
            var ext = Norm(extension);
            if (!CanConvert(ext))
                throw new ApiExceptionResponse($"Không hỗ trợ convert '{ext}' sang PDF qua ConvertAPI.", 400);

            if (string.IsNullOrWhiteSpace(_apiToken))
                throw new ApiExceptionResponse("ConvertAPI chưa được cấu hình: thiếu ConvertApi:ApiToken.", 500);

            Stream seekable = content;
            MemoryStream? buffered = null;
            if (!content.CanSeek)
            {
                buffered = new MemoryStream();
                await content.CopyToAsync(buffered, ct);
                buffered.Position = 0;
                seekable = buffered;
            }
            else
            {
                content.Position = 0;
            }

            try
            {
                var format = ext.TrimStart('.'); // "dwg" hoac "dwgx"
                using var form = new MultipartFormDataContent();
                var fileContent = new StreamContent(seekable);
                fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                form.Add(fileContent, "File", $"file{ext}");
                form.Add(new StringContent("Model"), "SpaceToConvert");
                form.Add(new StringContent("true"), "AutoFit");

                using var req = new HttpRequestMessage(HttpMethod.Post, $"/convert/{format}/to/pdf")
                {
                    Content = form,
                };
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiToken);

                using var res = await _http.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);
                if (!res.IsSuccessStatusCode)
                {
                    var snippet = body.Length > 300 ? body[..300] : body;
                    throw new ApiExceptionResponse(
                        $"ConvertAPI convert DWG->PDF thất bại (HTTP {(int)res.StatusCode}): {snippet}",
                        (int)res.StatusCode >= 500 ? 502 : 400);
                }

                using var doc = JsonDocument.Parse(body);
                var filesEl = doc.RootElement.GetProperty("Files");
                if (filesEl.ValueKind != JsonValueKind.Array || filesEl.GetArrayLength() == 0)
                    throw new ApiExceptionResponse("ConvertAPI không trả về file PDF nào.", 502);

                var base64Data = filesEl[0].GetProperty("FileData").GetString()
                    ?? throw new ApiExceptionResponse("ConvertAPI trả về FileData rỗng.", 502);

                var pdfBytes = Convert.FromBase64String(base64Data);
                var output = new MemoryStream(pdfBytes);
                output.Position = 0;
                return output;
            }
            finally
            {
                if (buffered != null)
                    await buffered.DisposeAsync();
            }
        }

        private static string Norm(string ext)
        {
            if (string.IsNullOrWhiteSpace(ext)) return string.Empty;
            ext = ext.Trim().ToLowerInvariant();
            return ext.StartsWith('.') ? ext : "." + ext;
        }
    }
}
