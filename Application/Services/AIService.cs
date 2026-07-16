using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Application.Services
{
    public class AIService : IAIService
    {
        private readonly IFileContentReader _fileReader;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<OllamaOptions> _options;

        public AIService(IFileContentReader fileReader, IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _fileReader = fileReader;
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task<string?> SummarizeContentAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var extractedFile = await _fileReader.LoadTextAsync(fileItemId, ct);
            if (extractedFile == null)
                throw new ApiExceptionResponse("File not found or could not be read");

            var content = extractedFile.Text;
            if (string.IsNullOrWhiteSpace(content))
                return null; // PDF scan / không trích được chữ -> không có gì để tóm tắt

            // Cắt bớt: đủ nắm ý chính, tránh treo Ollama CPU (generate ~10s/call).
            const int MaxContentChars = 6000;
            var sample = content.Length > MaxContentChars ? content[..MaxContentChars] : content;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/generate";

                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    SummarizeContentPrompt(extractedFile.Item.Name, sample),
                    Stream: false,
                    Think: false,
                    Format: new
                    {
                        type = "object",
                        properties = new { summary = new { type = "string" } },
                        required = new[] { "summary" }
                    },
                    Options: new GenerateOptions(0.3, 500));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                    return null; // advisory: AI lỗi thì bỏ qua, không chặn flow

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                var parsed = JsonSerializer.Deserialize<SummaryJson>(envelope?.Response ?? "", JsonOpts);
                var summary = parsed?.Summary?.Trim();
                return string.IsNullOrWhiteSpace(summary) ? null : summary;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string SummarizeContentPrompt(string fileName, string content) =>
            "Bạn tóm tắt tài liệu xây dựng cho người dùng đọc nhanh.\n" +
            $"Tên file: {fileName}\n" +
            "Trích nội dung (có thể lỗi khoảng cách/định dạng do trích xuất PDF — bỏ qua các lỗi đó):\n" +
            $"{content}\n\n" +
            "Yêu cầu summary (TIẾNG VIỆT, 1-3 câu, tối đa ~40 từ):\n" +
            "1) Câu đầu: Bỏ mấy câu rườm rà, mở đầu (Đây là, file này là,.... Kiểu 'File thiết kế', File quy định, File hợp đồng ). (hợp đồng, bản vẽ/thuyết minh, thông tư/quy định, báo cáo, biên bản...) về CHỦ ĐỀ gì.\n" +
            "2) Các câu sau: Câu miêu tả ngắn để người dùng đọc nhanh và vẫn nắm gọn nội dung chính.\n" +
            "KHÔNG bịa thông tin không có trong trích đoạn. KHÔNG nhận xét chất lượng tài liệu.";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };

        private record SummaryJson([property: JsonPropertyName("summary")] string? Summary);

        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, object Format, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict);
        private record GenerateResponse(string? Response);
    }
}
