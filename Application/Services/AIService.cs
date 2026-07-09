using Application.DTOs.ResponseDTOs.Notification;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Options;
using Domain.Entities;
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
                //client.Timeout = TimeSpan.FromMinutes(5);
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

                // AIService chỉ CHECK và trả kết quả. Việc lưu cờ Warnning + tạo/gửi notification
                // do NameMatchContentBackgroundService (worker) đảm nhiệm -> tránh trùng lặp & tránh tạo notification hỏng.
                return new FileNameCheckResult(parsed.Match, parsed.Confidence, parsed.Reason);
            }
            catch (Exception ex)
            {
                return new FileNameCheckResult(true, 0, $"Lỗi [{ex.GetType().Name}]: {ex.Message}");
            }
        }



        private static string CheckNameMatchesContentPromt(string fileName, string content) =>
            "Bạn kiểm tra xem TÊN FILE có mô tả đúng tài liệu bên trong không (phát hiện file đặt tên sai/nhầm).\n" +
            $"Tên file: {fileName}\n" +
            "Trích nội dung (có thể lỗi khoảng cách/định dạng do trích xuất PDF — bỏ qua các lỗi đó):\n" +
            $"{content}\n\n" +
            "Cách đánh giá (làm đúng thứ tự):\n" +
            "1) Nội dung là LOẠI tài liệu gì (hợp đồng, bản vẽ/thuyết minh thiết kế, quy định/thông tư, đề nghị tuyển dụng, báo cáo, biên bản…) và về CHỦ ĐỀ gì?\n" +
            "2) Tên file tuyên bố đây là loại tài liệu gì, chủ đề gì?\n" +
            "3) match=true nếu tên mô tả đúng hoặc gần đúng LOẠI + CHỦ ĐỀ của nội dung — chấp nhận viết tắt, tên thiếu chi tiết, hoặc tên chỉ nêu một phần/căn cứ trực tiếp của nội dung.\n" +
            "4) match=false nếu tên tuyên bố LOẠI TÀI LIỆU KHÁC hoặc CHỦ ĐỀ KHÁC với nội dung (vd: tên là hồ sơ thiết kế nhưng nội dung là đề nghị tuyển dụng; tên là quy định PCCC nhưng nội dung là hợp đồng). 'Cùng công ty' hay 'cùng dự án' KHÔNG đủ để coi là khớp.\n" +
            "KHÔNG đánh giá chất lượng/độ đầy đủ của tài liệu. KHÔNG bắt lỗi chính tả, in hoa, xuống dòng.\n" +
            "reason: 1-2 câu tiếng Việt nêu nội dung là loại tài liệu gì và vì sao khớp/không khớp. confidence = độ chắc chắn về kết luận (0..1).";


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
