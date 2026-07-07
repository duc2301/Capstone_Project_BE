using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using static Application.Interfaces.IServices.IAIService;

namespace Application.Services
{
    public class AIService : IAIService
    {
        private readonly IFileContentReader _fileReader;
        private readonly IFileItemService _fileService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<OllamaOptions> _options;

        public AIService(IFileContentReader fileReader, IFileItemService fileService, IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _fileReader = fileReader;
            _fileService = fileService;
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task<FileNameCheckResult> CheckNameMatchesContentAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var extractedFile = await _fileReader.LoadTextAsync(fileItemId, ct);
            if (extractedFile == null)
            {
                throw new ApiExceptionResponse("File not found or could not be read");
            }

            var fileName = extractedFile.Item.Name;
            var content = extractedFile.Text;

            if (string.IsNullOrWhiteSpace(content))
                return new FileNameCheckResult(true, 0, "Không đọc được nội dung file (có thể PDF scan) — bỏ qua kiểm tra.");



            // Implement your logic to check if the file name matches the content
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);
                var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/generate";

                //const int MaxContentChars = 40000;
                //var sample = content.Length > MaxContentChars ? content[..MaxContentChars] : content;

                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    CheckNameMatchesContentPromt(fileName, content),
                    Stream: false,
                    Think: false,
                    Format: new
                    {
                        type = "object",
                        properties = new
                        {
                            match = new { type = "boolean" },
                            confidence = new { type = "number" },
                            reason = new { type = "string" }
                        },
                        required = new[] { "match", "confidence", "reason" }
                    },
                    Options: new GenerateOptions(0.2, 400));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                    return new FileNameCheckResult(true, 0, "Không gọi được AI để kiểm tra.");

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                //var raw = envelope?.Response ?? "";
                ////raw = Regex.Replace(raw, "<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                ////raw = Regex.Replace(raw, "<think>.*$", "", RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();

                //// model hay thêm chữ quanh JSON -> lấy khối { ... }
                //var json = ExtractJsonObject(raw);
                //if (json is null)
                //    return new FileNameCheckResult(true, 0, "AI trả về không đúng định dạng — bỏ qua.");

                var parsed = JsonSerializer.Deserialize<NameCheckJson>(envelope?.Response ?? "", JsonOpts);
                if (parsed is null)
                    return new FileNameCheckResult(true, 0, "AI trả về rỗng — bỏ qua.");

                var matches = parsed.Match;
                return new FileNameCheckResult(matches, parsed?.Confidence ?? 0, parsed?.Reason);
            }
            catch (Exception ex)
            {
                return new FileNameCheckResult(true, 0, $"Lỗi [{ex.GetType().Name}]: {ex.Message}");
            }
        }



        private static string CheckNameMatchesContentPromt(string fileName, string content) =>
            "Bạn kiểm tra SƠ BỘ xem file có bị đặt tên sai hoàn toàn so với nội dung không (để phát hiện file rác/nhầm).\n" +
            $"Tên file: {fileName}\n" +
            "Trích nội dung (có thể lỗi khoảng cách/định dạng do trích xuất PDF — HÃY BỎ QUA các lỗi đó):\n" +
            $"{content}\n\n" +
            "Nguyên tắc:\n" +
            "- CHỈ xét CHỦ ĐỀ/LĨNH VỰC có liên quan không. TUYỆT ĐỐI KHÔNG đánh giá chất lượng, độ đầy đủ, hay tính 'chuẩn mực' của tài liệu.\n" +
            "- match=true nếu nội dung liên quan tới tên dù chỉ đại khái/gián tiếp (cùng lĩnh vực, cùng dự án, là căn cứ/thành phần của nhau).\n" +
            "- match=false CHỈ khi nội dung RÕ RÀNG không liên quan gì (ảnh, file rác, hoặc chủ đề khác hẳn lĩnh vực — vd tên về xây dựng nhưng nội dung là công thức nấu ăn).\n" +
            "- Phân vân → match=true. Bỏ qua lỗi chính tả, in hoa, xuống dòng, khoảng cách chữ.\n" +
            "confidence = độ chắc chắn (0..1). reason = nội dung ngắn. " +
            "Có thể trả về thêm một vài cảnh báo về nội dung trong file không liên quan khả nghi cần xem xét lại mặc dù file đa phần là khớp.(Chỉ trả lời thêm khi có dấu hiệu)";


        private static readonly JsonSerializerOptions JsonOpts = new() 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private record NameCheckJson(
            [property: JsonPropertyName("match")] bool Match,
            [property: JsonPropertyName("confidence")] double Confidence,
            [property: JsonPropertyName("reason")] string? Reason);

        private static string? ExtractJsonObject(string s)
        {
            int a = s.IndexOf('{'); int b = s.LastIndexOf('}');
            return (a >= 0 && b > a) ? s.Substring(a, b - a + 1) : null;
        }

        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, object Format, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict);
        private record GenerateResponse(string? Response);
    }
}
