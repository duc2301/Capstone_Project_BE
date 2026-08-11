using Application.DTOs.ResponseDTOs.Ai;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Options;
using Domain.Entities;
using Microsoft.Extensions.Logging;
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
        private readonly IUnitOfWork _unitOfWork;
        private readonly IOptions<OllamaOptions> _options;
        private readonly ILogger<AIService> _logger;

        public AIService(IFileContentReader fileReader, IFileTextExtractor extractor, IHttpClientFactory httpClientFactory, IUnitOfWork unitOfWork, IOptions<OllamaOptions> options, ILogger<AIService> logger)
        {
            _fileReader = fileReader;
            _extractor = extractor;
            _httpClientFactory = httpClientFactory;
            _unitOfWork = unitOfWork;
            _options = options;
            _logger = logger;
        }

        public async Task<ContentAnalysisResult?> AnalyzeContentAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var extractedFile = await _fileReader.LoadTextAsync(fileItemId, ct);
            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(extractedFile?.Item.FolderId);
            var project = await _unitOfWork.Repository<Project>().GetByIdAsync(folder?.ProjectId);
            if (extractedFile == null)
                throw new ApiExceptionResponse("File not found or could not be read");

            var content = extractedFile.Text;
            if (string.IsNullOrWhiteSpace(content))
                return null;

            //const int MaxContentChars = 6000;
            //var sample = content.Length > MaxContentChars ? content[..MaxContentChars] : content;

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/generate";

                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    AnalyzeContentPrompt(project.ProjectName, project.ProjectDescription, folder?.Name, content),
                    Stream: false,
                    Think: false,
                    Format: new
                    {
                        type = "object",
                        properties = new
                        {
                            summary = new { type = "string" },
                            suspicious = new { type = "boolean" },
                            reason = new { type = "string" }
                        },
                        required = new[] { "summary", "suspicious" }
                    },
                    Options: new GenerateOptions(0.3, 500));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama analyze trả về {StatusCode} cho file {FileItemId}", response.StatusCode, fileItemId);
                    return null;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                var parsed = JsonSerializer.Deserialize<AnalysisJson>(envelope?.Response ?? "", JsonOpts);
                if (parsed is null)
                    return null;

                var summary = parsed.Summary?.Trim();
                return new ContentAnalysisResult
                {
                    Summary = string.IsNullOrWhiteSpace(summary) ? null : summary,
                    Suspicious = parsed.Suspicious,
                    Reason = parsed.Reason?.Trim()
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Phân tích nội dung AI thất bại cho file {FileItemId}", fileItemId);
                return null;
            }
        }

        public async Task<BepParseResultDTO> ParseBepAsync(Stream content, string format, CancellationToken ct = default)
        {
            if (!_extractor.CanExtract(format))
                throw new ApiExceptionResponse($"Định dạng '{format}' không được hỗ trợ để đọc BEP.", 400);

            var text = await _extractor.ExtractTextAsync(content, format, ct);

            if (string.IsNullOrWhiteSpace(text))
                return new BepParseResultDTO { ExtractionEmpty = true };
            
            text = StripRepeatedLines(text);

            try
            {
                var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromMinutes(10);
                var url = $"{_options.Value.BaseUrl!.TrimEnd('/')}/api/generate";

                var payload = new GenerateRequest(
                    _options.Value.ChatModel,
                    BepParsePrompt(text),
                    Stream: false,
                    Think: false,
                    Format: BepFormatSchema,
                    Options: new GenerateOptions(0.2, -1));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync(ct);
                    _logger.LogError("Ollama parse BEP trả về {StatusCode}: {Body}", response.StatusCode, body);
                    throw new ApiExceptionResponse("Dịch vụ AI đang không phản hồi, vui lòng thử lại.", 502);
                }

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);
                var parsed = JsonSerializer.Deserialize<BepParseResultDTO>(envelope?.Response ?? "", JsonOpts);
                if (parsed is null)
                {
                    _logger.LogError("Ollama parse BEP trả về nội dung rỗng/không đúng schema: {Response}", envelope?.Response);
                    throw new ApiExceptionResponse("Không xử lý được kết quả AI, vui lòng thử lại.", 502);
                }
                return parsed;
            }
            catch (ApiExceptionResponse)
            {
                throw;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (JsonException ex)
            {                
                _logger.LogError(ex, "Kết quả AI không phải JSON hợp lệ khi parse BEP");
                throw new ApiExceptionResponse("Không xử lý được kết quả AI, vui lòng thử lại.", 502);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi gọi AI parse BEP");
                throw new ApiExceptionResponse("Đọc BEP thất bại, vui lòng thử lại.", 502);
            }
        }

        // Bỏ các dòng ngắn lặp lại nhiều lần (header/footer trang, watermark) — giữ nội dung thật.
        // GENERIC: dựa trên tần suất lặp, không phụ thuộc nội dung/nhãn của bất kỳ file nào.
        private static string StripRepeatedLines(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
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
                    continue;
                sb.Append(l).Append('\n');
            }
            return sb.ToString();
        }

        private static string BepParsePrompt(string content) =>
            "Bạn là trợ lý trích xuất thông tin từ tài liệu BEP (BIM Execution Plan / Kế hoạch thực hiện BIM) để điền form khởi tạo dự án.\n" +
            "MỖI BEP CÓ CẤU TRÚC, CÁCH ĐÁNH MỤC VÀ ĐẶT NHÃN KHÁC NHAU. Hãy hiểu Ý NGHĨA nội dung để trích, KHÔNG phụ thuộc số mục hay nhãn cố định. Tìm thông tin ở BẤT KỲ ĐÂU trong tài liệu.\n\n" +

            "XỬ LÝ VĂN BẢN: text được trích tự động từ file (PDF/DOCX) nên nhiều từ có thể DÍNH LIỀN (mất dấu cách), và nội dung trong bảng bị trải thành từng dòng: nhãn ở một dòng, giá trị ở dòng ngay sau. Hãy tự tách từ, tự ghép nhãn với giá trị, và trả về giá trị chuẩn hoá (có dấu cách, đúng chính tả), GIỮ ĐÚNG nội dung gốc — không đổi tên riêng, không bịa.\n\n" +

            "TÀI LIỆU:\n" +
            $"{content}\n\n" +
            "TRÍCH CÁC TRƯỜNG SAU (theo Ý NGHĨA):\n" +
            "- projectName: tên dự án / công trình. Trường quan trọng nhất — hầu như BEP nào cũng có, cố gắng luôn tìm ra.\n" +
            "- projectCode: mã / ký hiệu dự án nếu có; không có để trống.\n" +
            "- projectDescription: 1-2 câu mô tả loại công trình và mục tiêu áp dụng BIM, dựa trên nội dung; không bịa số liệu.\n" +
            "- ownerOrganizationName: tên tổ chức Chủ đầu tư / Chủ sở hữu dự án (bên đặt hàng/thuê thực hiện).\n" +
            "- address: địa điểm / địa chỉ CÔNG TRÌNH (nơi thi công/xây dựng).\n" +
            "- contactAddress: địa chỉ LIÊN HỆ / giao dịch (trụ sở, văn phòng chủ đầu tư, Ban QLDA, đầu mối liên hệ). Nếu tài liệu có một mục/ô mang nghĩa 'địa chỉ liên hệ' thì TRÍCH NGUYÊN VĂN giá trị đó, kể cả khi ghi ngắn gọn (chỉ quận/thành phố) hay không đầy đủ số nhà — vẫn tính là có. Chỉ để trống khi tài liệu KHÔNG hề nhắc tới địa chỉ liên hệ. Đây là trường KHÁC address: address = nơi xây dựng công trình, contactAddress = nơi liên hệ; không được điền trùng giá trị của address.\n" +
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
                address = new { type = "string" },
                contactAddress = new { type = "string" },
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

            required = new[]
            {
                "projectName", "projectCode", "projectDescription", "ownerOrganizationName",
                "address", "contactAddress", "groups", "packages"
            }
        };

        private static string AnalyzeContentPrompt(string projectName, string projectDescription, string? folderName, string content) =>
            "Bạn phân tích tài liệu xây dựng: Tóm tắt nội dung VÀ kiểm tra nội dung có đúng loại/chủ đề liên quan không.\n" +
            $"Tên dự án: {projectName}\n" +
            $"Mô tả dự án: {projectDescription}\n" +
            $"Tên thư mục chứa nội dung: {folderName}\n" +
            "Trích nội dung (có thể lỗi khoảng cách/định dạng do trích xuất PDF — BỎ QUA các lỗi đó, KHÔNG vì lỗi trích xuất mà coi là nghi ngờ):\n" +
            $"{content}\n\n" +
            "Trả 3 trường:\n" +
            "1) summary (TIẾNG VIỆT, 1-3 câu, ~40 từ): loại tài liệu + chủ đề chính; bỏ mở đầu rườm rà ('Đây là', 'File này là'). KHÔNG bịa, KHÔNG nhận xét chất lượng.\n" +
            "2) suspicious (boolean): true CHỈ KHI nội dung RÕ RÀNG không liên quan tới tên tệp / không phải tài liệu xây dựng - dự án (vd tên nói 'bản vẽ kết cấu' nhưng nội dung là truyện, hoá đơn cá nhân, nội dung rác, thông tin từ các lĩnh vực không liên quan như tuyển dụng, học tập). KHOAN DUNG: chỉ báo khi lệch trắng trợn; nghi ngờ nhẹ hoặc chỉ khác định dạng -> false. Xét LOẠI + CHỦ ĐỀ, cùng công ty/dự án chưa đủ để coi là khớp.\n" +
            "3) reason (TIẾNG VIỆT, 1 câu): CHỈ khi suspicious=true, nêu vì sao lệch; suspicious=false thì để trống.\n" +
            "Chỉ trả JSON đúng schema, không kèm chữ nào khác.";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString
        };


        private record AnalysisJson(
            [property: JsonPropertyName("summary")] string? Summary,
            [property: JsonPropertyName("suspicious")] bool Suspicious,
            [property: JsonPropertyName("reason")] string? Reason);

        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, object Format, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict);
        private record GenerateResponse(string? Response);
    }
}
