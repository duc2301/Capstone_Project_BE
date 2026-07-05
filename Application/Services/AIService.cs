using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Net.Http.Json;
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
                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    CheckNameMatchesContentPromt(fileName, content),
                    Stream: false,
                    Think: false,
                    Options: new GenerateOptions(0.2, 300));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                    return new FileNameCheckResult(true, 0, "Không gọi được AI để kiểm tra.");

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                var raw = envelope?.Response ?? "";
                raw = Regex.Replace(raw, "<think>.*?</think>", "", RegexOptions.Singleline | RegexOptions.IgnoreCase);
                raw = Regex.Replace(raw, "<think>.*$", "", RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();

                // model hay thêm chữ quanh JSON -> lấy khối { ... }
                var json = ExtractJsonObject(raw);
                if (json is null)
                    return new FileNameCheckResult(true, 0, "AI trả về không đúng định dạng — bỏ qua.");

                var parsed = JsonSerializer.Deserialize<NameCheckJson>(json, JsonOpts);

                // tự áp ngưỡng trong code, đừng tin 100% cờ boolean của model
                var matches = parsed is not null && parsed.Confidence >= 0.75 && parsed.Match;
                return new FileNameCheckResult(matches, parsed?.Confidence ?? 0, parsed?.Reason);
            }
            catch (Exception ex)
            {
                return new FileNameCheckResult(true, 0, $"Lỗi [{ex.GetType().Name}]: {ex.Message}");
            }
        }



        private static string CheckNameMatchesContentPromt(string fileName, string fileContent) =>
           "Bạn đang lập chỉ mục tìm kiếm cho tài liệu trong hệ thống CDE xây dựng.\n" +
           $"Tên file: {fileName}\n" +
           $"Nội dung: {fileContent}\n\n" +
           $"Nhiệm vụ: Kiểm tra nghiêm ngặt xem nội dung có khớp với tên file không? confidence>0.75 thì mới trả về match=true" +
            "Ghi chú: Lý do thì giải thích nội dung file đang nói về cái gì, lý do mà nó không hợp lệ." +
           $"Trả về DUY NHẤT JSON: {{\"match\": true/false, \"confidence\": 0..1, \"reason\": \"ngắn gọn tiếng Việt\"}}";


        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        private record NameCheckJson(
            [property: JsonPropertyName("match")] bool Match,
            [property: JsonPropertyName("confidence")] double Confidence,
            [property: JsonPropertyName("reason")] string? Reason);

        private static string? ExtractJsonObject(string s)
        {
            int a = s.IndexOf('{'); int b = s.LastIndexOf('}');
            return (a >= 0 && b > a) ? s.Substring(a, b - a + 1) : null;
        }

        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict);
        private record GenerateResponse(string? Response);
    }
}
