using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Adapters.Ai
{
    public class AIService : IAIService
    {
        private readonly IFileContentReader _fileReader;
        private readonly IFileTextExtractor _extractor;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<OllamaOptions> _options;

        public AIService(IFileContentReader fileReader, IFileTextExtractor extractor, IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _fileReader = fileReader;
            _extractor = extractor;
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

        public async Task<BepParseResultDTO> ParseBepAsync(Stream content, string format, CancellationToken ct = default)
        {
            if (!_extractor.CanExtract(format))
                return new BepParseResultDTO { ExtractionEmpty = true };

            string text;
            try
            {
                text = await _extractor.ExtractTextAsync(content, format, ct);
            }
            catch (Exception)
            {
                return new BepParseResultDTO { ExtractionEmpty = true };
            }

            if (string.IsNullOrWhiteSpace(text))
                return new BepParseResultDTO { ExtractionEmpty = true };

            // Chỉ làm sạch GENERIC (không phụ thuộc cấu trúc từng file): bỏ header/footer/watermark lặp lại.
            text = StripRepeatedLines(text);

            try
            {
                var client = _httpClientFactory.CreateClient();
                // Ollama chạy CPU + input BEP dài -> generate có thể >100s. HttpClient mặc định timeout 100s
                // sẽ hủy giữa chừng và rơi vào catch (trả extractionEmpty sai). Nới rộng cho đủ.
                client.Timeout = TimeSpan.FromMinutes(5);
                var url = $"{_options.Value.BaseUrl!.TrimEnd('/')}/api/generate";

                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    BepParsePrompt(text),
                    Stream: false,
                    Think: false,
                    Format: BepFormatSchema,
                    Options: new GenerateOptions(0.2, 800));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                    return new BepParseResultDTO { ExtractionEmpty = true };

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                var parsed = JsonSerializer.Deserialize<BepParseResultDTO>(envelope?.Response ?? "", JsonOpts);
                return parsed ?? new BepParseResultDTO { ExtractionEmpty = true };
            }
            catch (Exception)
            {
                return new BepParseResultDTO { ExtractionEmpty = true };
            }
        }

        // Bỏ các dòng ngắn lặp lại nhiều lần (header/footer trang, watermark) — giữ nội dung thật.
        // GENERIC: dựa trên tần suất lặp, không phụ thuộc nội dung/nhãn của bất kỳ file nào.
        private static string StripRepeatedLines(string text)
        {
            var lines = text.Split('\n');
            var freq = new Dictionary<string, int>();
            foreach (var l in lines)
            {
                var key = l.Trim();
                if (key.Length is > 3 and < 80)
                    freq[key] = freq.TryGetValue(key, out var c) ? c + 1 : 1;
            }

            var sb = new StringBuilder(text.Length);
            foreach (var l in lines)
            {
                var key = l.Trim();
                if (key.Length is > 3 and < 80 && freq.TryGetValue(key, out var c) && c >= 5)
                    continue; // lặp >=5 lần -> boilerplate, bỏ
                sb.Append(l).Append('\n');
            }
            return sb.ToString();
        }

        private static string BepParsePrompt(string content) =>
            "Bạn là trợ lý trích xuất thông tin từ tài liệu BEP (BIM Execution Plan / Kế hoạch thực hiện BIM) để điền form khởi tạo dự án.\n" +
            "MỖI BEP CÓ CẤU TRÚC, CÁCH ĐÁNH MỤC VÀ ĐẶT NHÃN KHÁC NHAU. Hãy hiểu Ý NGHĨA nội dung để trích, KHÔNG phụ thuộc số mục hay nhãn cố định. Tìm thông tin ở BẤT KỲ ĐÂU trong tài liệu.\n\n" +

            "XỬ LÝ VĂN BẢN: text trích từ PDF nên nhiều từ có thể DÍNH LIỀN (mất dấu cách). Hãy tự tách từ và trả về giá trị chuẩn hoá (có dấu cách, đúng chính tả), GIỮ ĐÚNG nội dung gốc — không đổi tên riêng, không bịa.\n\n" +

            "TÀI LIỆU:\n" +
            $"{content}\n\n" +

            "TRÍCH CÁC TRƯỜNG SAU (theo Ý NGHĨA):\n" +
            "- projectName: tên dự án / công trình. Trường quan trọng nhất — hầu như BEP nào cũng có, cố gắng luôn tìm ra.\n" +
            "- projectCode: mã / ký hiệu dự án nếu có; không có để trống.\n" +
            "- projectDescription: 1-2 câu mô tả loại công trình và mục tiêu áp dụng BIM, dựa trên nội dung; không bịa số liệu.\n" +
            "- ownerOrganizationName: tên tổ chức Chủ đầu tư / Chủ sở hữu dự án (bên đặt hàng/thuê thực hiện).\n" +
            "- address: địa điểm / địa chỉ công trình.\n" +
            "- contactAddress: địa chỉ liên hệ RIÊNG nếu tài liệu có phân biệt (khác địa điểm công trình). Nếu chỉ có 1 địa chỉ thì để TRỐNG trường này (đừng lặp lại address).\n" +
            "- groups: DANH SÁCH CÁC BÊN THAM GIA / NHÓM LÀM VIỆC của dự án. Trích ĐẦY ĐỦ mọi bên tài liệu nêu — dù xuất hiện ở bảng phân công trách nhiệm, bảng phân quyền môi trường dữ liệu chung (CDE), sơ đồ tổ chức, hay bảng phối hợp. Các loại bên thường gặp (chỉ là gợi ý, không bắt buộc phải có): chủ đầu tư, tư vấn thiết kế (kiến trúc/kết cấu/MEP), nhà thầu thi công, tư vấn giám sát/thẩm tra, quản lý/điều phối BIM. Gộp bên trùng tên. Mỗi phần tử: name (tên bên), description (vai trò ngắn nếu tài liệu nêu), partnerOrganizationName (tên công ty cụ thể đảm nhận bên đó CHỈ khi tài liệu ghi rõ, không chắc để trống). Thông tin của nhóm tham gia nó là các bên làm việc bên trong 1 CDE, chứ ko phải tổng hợp thông tin các mục khác rồi suy diễn (thông tin các nhóm làm việc thường nằm trong mục lớn ' MÔI TRƯỜNG LÀM VIỆC CHUNG CDE' có thể là bảng với các phân quyền R, W, N). Lưu ý không được gộp các nhóm. Liệt kê trong file bao nhiêu nhóm thì sẽ để nguyên bấy nhiêu nhóm, ko gộp chung vài nhóm tương đồng (ví dụ như có nhiều bên tư vấn khác nhau (tư vấn MEP, tư vấn thiết kế) thì cũng tách ra) quyền hoặc trùng quyền hạn\n" +
            "- packages: gói thầu / hợp đồng — CHỈ tạo khi tài liệu nêu rõ gói thầu có tên (kèm giá trị hợp đồng, đơn vị tiền, nhà thầu nếu có). Nếu tài liệu chỉ mô tả phạm vi công việc/sản phẩm mà không có gói thầu -> MẢNG RỖNG. Không bịa gói thầu.\n\n" +

            "QUY TẮC:\n" +
            "- KHÔNG bịa. Trường không tìm thấy -> chuỗi rỗng; danh sách không có -> mảng rỗng.\n" +
            "- Bỏ qua các bảng kỹ thuật thuần tuý không chứa các trường trên (ví dụ bảng mức độ phát triển thông tin/LOD, hệ toạ độ, quy ước đặt tên, danh sách phần mềm).\n" +
            "- Chỉ trả JSON đúng schema, không kèm chữ nào khác.";

        // JSON schema cho Ollama structured output (field Format của /api/generate).
        private static readonly object BepFormatSchema = new
        {
            type = "object",
            properties = new
            {
                projectName = new { type = "string" },
                projectCode = new { type = "string" },
                projectDescription = new { type = "string" },
                ownerOrganizationName = new { type = "string" },
                contactAddress = new { type = "string" },
                address = new { type = "string" },
                groups = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            description = new { type = "string" },
                            partnerOrganizationName = new { type = "string" }
                        },
                        required = new[] { "name" }
                    }
                },
                packages = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string" },
                            description = new { type = "string" },
                            contractValue = new { type = "number" },
                            currency = new { type = "string" },
                            contractorOrganizationName = new { type = "string" }
                        },
                        required = new[] { "name" }
                    }
                }
            },
            required = new[] { "projectName" }
        };

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
