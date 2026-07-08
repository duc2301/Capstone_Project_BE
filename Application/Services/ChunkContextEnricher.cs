using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Application.Services
{
    public class ChunkContextEnricher : IChunkContextEnricher
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<OllamaOptions> _options;

        public ChunkContextEnricher(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
        }

        public async Task<string?> EnrichAsync(string fileMeta, string parentContent, CancellationToken ct = default)
        {
            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(5);

                var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/generate";
                var payload = new GenerateRequest(
                    _options.Value.SubModel,
                    BuildPrompt(fileMeta, parentContent),
                    Stream: false,
                    Think: false,
                    Options: new GenerateOptions(0.2, 128));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                return Clean(result?.Response);
            }
            catch (Exception ex)
            {
                return null;
            }
        }

        private static string BuildPrompt(string fileMeta, string parent) =>
            "Bạn đang lập chỉ mục tìm kiếm cho tài liệu trong hệ thống CDE xây dựng.\n" +
            $"Metadata tài liệu: {fileMeta}\n" +
            "Đoạn nội dung cần định vị:\n" +
            $"{parent}\n\n" +
            "Nhiệm vụ: viết 1–2 câu TIẾNG VIỆT nêu ngắn gọn đoạn này nói về gì và thuộc phần nào " +
            "của tài liệu, nhằm cải thiện tìm kiếm.\n" +
            "Chỉ trả về đúng câu tiếng Việt đó — KHÔNG thêm nhãn, KHÔNG dùng tiếng Anh, hỉ đưa ra kết quả, KHÔNG thêm bất kỳ lời mở đầu, lời giải thích hay câu kết luận nào";

        private static string? Clean(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var s = Regex.Replace(raw, "<think>.*?</think>", "",
                        RegexOptions.Singleline | RegexOptions.IgnoreCase).Trim();
            return s.Length == 0 ? null : s;
        }

        private static readonly JsonSerializerOptions JsonOpts =
            new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        // map JSON Ollama
        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict);
        private record GenerateResponse(string? Response);
    }
}
