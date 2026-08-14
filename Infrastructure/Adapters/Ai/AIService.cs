using Application.DTOs.ResponseDTOs.Ai;
using Application.DTOs.ResponseDTOs.Project;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Interfaces.IUnitOfWork;
using Application.Options;
using Application.Services.Ai;
using Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

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
        private readonly ITextChunker _chunker;

        public AIService(IFileContentReader fileReader, IFileTextExtractor extractor, IHttpClientFactory httpClientFactory, IUnitOfWork unitOfWork, IOptions<OllamaOptions> options, ILogger<AIService> logger, ITextChunker chunker)
        {
            _fileReader = fileReader;
            _extractor = extractor;
            _httpClientFactory = httpClientFactory;
            _unitOfWork = unitOfWork;
            _options = options;
            _logger = logger;
            _chunker = chunker;
        }

        public async Task<ContentAnalysisResult?> AnalyzeContentAsync(Guid fileItemId, CancellationToken ct = default)
        {
            var extractedFile = await _fileReader.LoadTextAsync(fileItemId, ct);
            // Kiểm null TRƯỚC khi deref: bản cũ đọc extractedFile?.Item.FolderId ở trên rồi mới
            // throw ở dưới — `?.` chỉ che extractedFile, `.Item` vẫn deref thẳng.
            if (extractedFile == null)
                throw new ApiExceptionResponse("File not found or could not be read");

            var folder = await _unitOfWork.Repository<Folder>().GetByIdAsync(extractedFile.Item.FolderId);
            var project = folder is null ? null : await _unitOfWork.Repository<Project>().GetByIdAsync(folder.ProjectId);

            var content = extractedFile.Text;
            if (string.IsNullOrWhiteSpace(content))
            {
                // Không log thì hỏng ở đây là vô hình: worker nền thấy null sẽ bỏ qua,
                // người dùng chỉ thấy file không có cảnh báo mà không có lỗi nào.
                _logger.LogWarning("Không trích được chữ từ file {FileItemId} ({Format}) — bỏ qua phân tích nội dung",
                    fileItemId, extractedFile.Version.Format);
                return null;   // null = CHƯA phân tích được, KHÁC hẳn "đã phân tích và thấy ổn"
            }

            // Chặn TRƯỚC khi gọi model: không đủ chữ thì vừa không tóm tắt được vừa không phán
            // đoán được. Để model tự xoay sở là mời nó lấy tên/mô tả dự án trong prompt viết thay.
            if (!ContentSignalPolicy.HasEnoughSignal(content))
            {
                var chars = content.Trim().Length;
                _logger.LogInformation("File {FileItemId} chỉ có {Chars} ký tự — gắn cờ, không gọi model", fileItemId, chars);
                return new ContentAnalysisResult
                {
                    Summary = null,
                    Suspicious = true,
                    Reason = $"Tệp gần như không có nội dung văn bản ({chars} ký tự) — không đủ cơ sở để tóm tắt hay đối chiếu với tên tệp."
                };
            }

            try
            {
                var client = _httpClientFactory.CreateClient();
                var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/generate";

                // Kiểm tra nội dung có liên quan hay không là bài toán phán đoán, khác hẳn việc
                // trích xuất theo schema của BEP — nên dùng model riêng. ChatModel đã hạ xuống 4b
                // để parse BEP cho nhanh, dùng chung sẽ làm cờ "nghi ngờ" kém hẳn đi.
                var payload = new GenerateRequest(
                    _options.Value.CheckModel ?? _options.Value.ChatModel,
                    AnalyzeContentPrompt(extractedFile.Item.Name, project?.ProjectName,
                                         project?.ProjectDescription, folder?.Name, Sample(content)),
                    Stream: false,
                    Think: false,
                    Format: AnalyzeSchema,
                    // 0.0 chứ không 0.3: kiểm duyệt cần TÁI LẬP — cùng một file phải ra cùng một cờ,
                    // chứ không phải mỗi lần upload lại một kết quả khác.
                    Options: new GenerateOptions(0.0, 600, _options.Value.NumCtx, _options.Value.NumGpu));

                var response = await client.PostAsync(url,
                    new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"),
                    ct);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Ollama analyze trả về {StatusCode} cho file {FileItemId}", response.StatusCode, fileItemId);
                    return null;
                }

                var envelope = await response.Content.ReadFromJsonAsync<GenerateResponse>(JsonOpts, ct);

                // Hết ngân sách token giữa chừng -> JSON vỡ hoặc thiếu trường. CallBepAsync có
                // check này, nhánh phân tích nội dung thì trước giờ không.
                if (envelope?.DoneReason == "length")
                {
                    _logger.LogWarning("Ollama analyze bị cắt do hết token cho file {FileItemId}", fileItemId);
                    return null;
                }

                var parsed = JsonSerializer.Deserialize<AnalysisJson>(envelope?.Response ?? "", JsonOpts);
                if (parsed is null)
                    return null;

                // KIỂM CHỨNG BẰNG CODE, không tin lời model: các cụm nó bảo "trích nguyên văn"
                // phải THẬT SỰ nằm trong file. Không cụm nào khớp = nó sinh từ ngữ cảnh prompt
                // (tên/mô tả dự án) chứ không từ nội dung -> vứt tóm tắt và bật cờ.
                if (!QuotesAreGrounded(parsed.Evidence, content))
                {
                    _logger.LogWarning("File {FileItemId}: dẫn chứng của model không có trong nội dung — loại tóm tắt", fileItemId);
                    return new ContentAnalysisResult
                    {
                        Summary = null,
                        Suspicious = true,
                        Reason = "Không đối chiếu được nội dung tệp với kết quả phân tích của AI — cần người kiểm tra."
                    };
                }

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

        // num_ctx=17000 token ≈ 35-40k ký tự tiếng Việt. Vượt ngưỡng, Ollama cắt ÂM THẦM đuôi
        // prompt và model vẫn trả JSON hợp lệ -> không có cách nào biết đã mất chữ. Tóm tắt không
        // cần đọc hết: lấy đầu (loại tài liệu, tiêu đề) + đuôi (kết luận, phụ lục).
        private const int HeadChars = 9000;
        private const int TailChars = 3000;

        private static string Sample(string content) =>
            content.Length <= HeadChars + TailChars
                ? content
                : content[..HeadChars] + "\n[...lược bớt phần giữa...]\n" + content[^TailChars..];

        // Chỉ cần MỘT cụm khớp là đủ chứng minh model có đọc file; đòi khớp hết sẽ rụng oan vì
        // text trích xuất hay dính chữ và model chuẩn hoá lại khoảng trắng khi copy.
        private static bool QuotesAreGrounded(IReadOnlyList<string>? quotes, string content)
        {
            if (quotes is null || quotes.Count == 0) return false;

            var haystack = Squash(content);
            foreach (var q in quotes)
            {
                if (string.IsNullOrWhiteSpace(q)) continue;
                var needle = Squash(q);
                // Cụm quá ngắn thì khớp ngẫu nhiên, không chứng minh được gì.
                if (needle.Length >= 8 && haystack.Contains(needle, StringComparison.Ordinal)) return true;
            }
            return false;
        }

        // Bỏ mọi thứ không phải chữ/số để dẫn chứng vẫn khớp khi model tự sửa khoảng trắng, dấu câu.
        private static string Squash(string s)
        {
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s.ToLowerInvariant())
                if (char.IsLetterOrDigit(ch)) sb.Append(ch);
            return sb.ToString();
        }

        public async Task<BepParseResultDTO> ParseBepAsync(Stream content, string format, CancellationToken ct = default)
        {
            if (!_extractor.CanExtract(format))
                throw new ApiExceptionResponse($"Định dạng '{format}' không được hỗ trợ để đọc BEP.", 400);

            var text = await _extractor.ExtractTextAsync(content, format, ct);

            if (string.IsNullOrWhiteSpace(text))
                return new BepParseResultDTO { ExtractionEmpty = true };
            
            text = StripRepeatedLines(text);

            // BEP thật cỡ 70-80 trang vượt xa num_ctx. Nhét một lượt thì prompt bị cắt âm thầm,
            // model chỉ đọc được phần đầu mà vẫn trả JSON hợp lệ -> mất dữ liệu không phát hiện được.
            var slices = _chunker.ChunkForExtraction(text);
            if (slices.Count == 0)
                return new BepParseResultDTO { ExtractionEmpty = true };

            try
            {
                // Pha 1 — 6 trường thông tin dự án chỉ xuất hiện ở phần đầu tài liệu
                // (trang bìa + mục thông tin chung), nên chỉ lát đầu mới hỏi đủ schema.
                var result = await CallBepAsync(BepInfoPrompt(slices[0]), BepInfoSchema, ct);
                if (result is null)
                {
                    _logger.LogError("Ollama parse BEP trả về nội dung rỗng/không đúng schema ở lát đầu");
                    throw new ApiExceptionResponse("Không xử lý được kết quả AI, vui lòng thử lại.", 502);
                }

                // Pha 1b — quét nhóm/gói thầu trên chính lát đầu, dùng schema hẹp như các lát sau.
                var firstParts = await CallBepAsync(BepPartsPrompt(slices[0]), BepPartsSchema, ct);
                if (firstParts is not null) MergeParts(result, firstParts);

                // Trường đơn chỉ được điền từ lát 1. Ranh giới lát đổi theo độ dài từng tài liệu,
                // nên mục thông tin chung có thể rơi sang lát 2 và mất vĩnh viễn (pha 2 không lấy
                // trường đơn). Chạy bù ĐÚNG MỘT lượt full schema trên lát 2, chỉ điền chỗ còn trống.
                if (slices.Count > 1 && HasMissingScalars(result))
                {
                    var fallback = await CallBepAsync(BepInfoPrompt(slices[1]), BepInfoSchema, ct);
                    if (fallback is not null) FillMissingScalars(result, fallback);
                }

                // Pha 2 — các lát sau chỉ quét nhóm/gói thầu. Schema hẹp lại vừa giảm token phải sinh
                // (nút thắt nằm ở tốc độ sinh) vừa bớt chỗ cho model bịa các trường không có mặt.
                for (int i = 1; i < slices.Count; i++)
                {
                    if (!MayContainParties(slices[i]) || IsNamingConventionSlice(slices[i])) continue;

                    var part = await CallBepAsync(BepPartsPrompt(slices[i]), BepPartsSchema, ct);
                    if (part is not null) MergeParts(result, part);
                }

                NormalizeBlanks(result);
                DedupeSelf(result);
                DropUnsupportedPackages(result);
                DropOwnerAsGroup(result);
                DropLienDanhMembers(result);
                DropPersonRoles(result);
                DropJunkGroups(result);

                if (result.Groups.Count > SuspiciousGroupCount)
                    _logger.LogWarning(
                        "Parse BEP: {Count} nhóm là bất thường — nhiều khả năng model đã quét nhầm một bảng tra cứu (mã vai trò đặt tên tệp, danh mục chức danh) thay vì bảng phân quyền CDE",
                        result.Groups.Count);

                _logger.LogInformation("Parse BEP xong: {Slices} lát, {Groups} nhóm, {Packages} gói thầu",
                    slices.Count, result.Groups.Count, result.Packages.Count);
                return result;
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

        // Một lượt gọi model cho một lát. Trả null khi lát không cho ra JSON dùng được;
        // pha 2 bỏ qua lát đó thay vì làm hỏng cả lần đọc.
        private async Task<BepParseResultDTO?> CallBepAsync(string prompt, object schema, CancellationToken ct)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(10);
            var url = $"{_options.Value.BaseUrl!.TrimEnd('/')}/api/generate";

            var payload = new GenerateRequest(
                _options.Value.ChatModel,
                prompt,
                Stream: false,
                Think: false,
                Format: schema,
                // temperature 0 = giải mã tham lam. Trích xuất không cần sáng tạo, mà cần TÁI LẬP:
                // đo trên BEP Viettel, temp 0.2 cho 3 danh sách khác nhau qua 3 lượt, temp 0 cho
                // 3 lượt giống hệt và là danh sách đúng nhất (đủ Kiến trúc/Kết cấu/MEP).
                Options: new GenerateOptions(0.0, -1, _options.Value.NumCtx, _options.Value.NumGpu));

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

            // Hết ngân sách token giữa chừng -> JSON chắc chắn vỡ. Báo đúng nguyên nhân,
            // đừng để rơi xuống Deserialize rồi ném JsonException không rõ vì sao.
            if (envelope?.DoneReason == "length")
            {
                _logger.LogError("Ollama parse BEP bị cắt do hết token: prompt={Prompt}, sinh={Eval}",
                    envelope.PromptEvalCount, envelope.EvalCount);
                throw new ApiExceptionResponse(
                    "Tài liệu BEP quá dài để xử lý trong một lượt. Cần chia nhỏ tài liệu.", 422);
            }

            return JsonSerializer.Deserialize<BepParseResultDTO>(envelope?.Response ?? "", JsonOpts);
        }

        // Sàng rẻ trước khi gọi model: phần lớn mục trong BEP là nội dung kỹ thuật thuần
        // (LOD, quy ước đặt tên, hệ toạ độ, danh sách phần mềm), không chứa nhóm/gói thầu nào.
        // Để danh sách rộng tay — bỏ sót một lát tốn kém hơn nhiều so với gọi thừa một lượt.
        private static readonly string[] PartyHints =
        {
            "nhóm", "tổ ", "bên tham gia", "các bên", "đơn vị", "nhà thầu", "gói thầu",
            "thầu phụ", "chủ đầu tư", "tư vấn", "tổ chức", "công ty", "phân công", "trách nhiệm"
        };

        private static bool MayContainParties(string slice)
        {
            foreach (var hint in PartyHints)
                if (slice.Contains(hint, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Mục quy ước đặt tên là nguồn nhiễu lớn nhất còn lại: model kéo "Đơn vị khởi tạo",
        // "Bên chủ trì", bảng mã vai trò từ đó ra làm bên tham gia. Prompt cấm hai lượt vẫn lọt,
        // nên chặn ở tầng chọn lát: lát nào thiên hẳn về đặt tên thì không quét.
        // Đo trên 5 BEP: luật này khớp đúng 1 lát (mục quy ước đặt tên của BEP 77 trang),
        // không chạm lát nào khác — kể cả BEP có nói về đặt tên nhưng chủ yếu là phân công.
        private static readonly string[] NamingConventionHints =
        {
            "quy tắc đặt tên", "quy ước đặt tên", "đặt tên tệp", "đặt tên tập tin",
            "trường mã dự án", "đơn vị khởi tạo", "originator", "trạng thái hiệu chỉnh",
            "naming convention"
        };

        // Dấu hiệu của bảng phân công/phân quyền — thứ ĐÁNG quét. Cố ý không dùng chung
        // PartyHints: "đơn vị" xuất hiện dày trong cả mục đặt tên nên không phân biệt được.
        private static readonly string[] AssignmentHints =
        {
            "phân công", "trách nhiệm", "phân quyền", "bên tham gia",
            "sơ đồ tổ chức", "phối hợp", "bộ phận", "chủ trì"
        };

        private static bool IsNamingConventionSlice(string slice)
        {
            int naming = CountHits(slice, NamingConventionHints);
            return naming >= 8 && naming > CountHits(slice, AssignmentHints);
        }

        private static int CountHits(string text, string[] hints)
        {
            int total = 0;
            foreach (var h in hints)
            {
                int i = 0;
                while ((i = text.IndexOf(h, i, StringComparison.OrdinalIgnoreCase)) >= 0)
                {
                    total++;
                    i += h.Length;
                }
            }
            return total;
        }

        // Gộp kết quả một lát vào kết quả chung.
        // Trường đơn không đụng tới: lát đầu là nguồn đáng tin nhất cho thông tin dự án.
        // Mảng: hợp nhất rồi khử trùng theo tên đã chuẩn hoá — bắt buộc, vì các lát có overlap
        // và vì cùng một nhóm thường được nhắc lại ở nhiều mục.
        private static void MergeParts(BepParseResultDTO target, BepParseResultDTO part)
        {
            foreach (var g in part.Groups ?? new())
            {
                if (string.IsNullOrWhiteSpace(g.Name)) continue;

                var existing = target.Groups.FirstOrDefault(x => SameName(x.Name, g.Name));
                if (existing is null) { target.Groups.Add(g); continue; }

                if (Longer(g.Description, existing.Description)) existing.Description = g.Description;
                existing.PartnerOrganizationName ??= g.PartnerOrganizationName;
            }

            foreach (var p in part.Packages ?? new())
            {
                if (string.IsNullOrWhiteSpace(p.Name)) continue;

                var existing = target.Packages.FirstOrDefault(x => SameName(x.Name, p.Name));
                if (existing is null) { target.Packages.Add(p); continue; }

                if (Longer(p.Description, existing.Description)) existing.Description = p.Description;
                existing.ContractValue ??= p.ContractValue;
                existing.Currency ??= p.Currency;
                existing.ContractorOrganizationName ??= p.ContractorOrganizationName;
            }
        }

        // Pha 1 có schema bắt buộc nên model điền "" cho trường không tìm thấy, pha 2 thì để null.
        // Trả về FE hai kiểu rỗng khác nhau trong cùng một trường là mời lỗi — quy hết về null.
        private static void NormalizeBlanks(BepParseResultDTO r)
        {
            r.ProjectName = Blank(r.ProjectName);
            r.ProjectCode = Blank(r.ProjectCode);
            r.ProjectDescription = Blank(r.ProjectDescription);
            r.OwnerOrganizationName = Blank(r.OwnerOrganizationName);
            r.Address = Blank(r.Address);
            r.ContactAddress = Blank(r.ContactAddress);

            foreach (var g in r.Groups)
            {
                g.Name = g.Name?.Trim() ?? string.Empty;
                g.Description = Blank(g.Description);
                g.PartnerOrganizationName = Blank(g.PartnerOrganizationName);
            }

            foreach (var p in r.Packages)
            {
                p.Name = p.Name?.Trim() ?? string.Empty;
                p.Description = Blank(p.Description);
                p.Currency = Blank(p.Currency);
                p.ContractorOrganizationName = Blank(p.ContractorOrganizationName);
            }
        }

        private static string? Blank(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

        private const int SuspiciousGroupCount = 15;

        // Trường đơn nào còn trống thì lấy từ lát chạy bù; đã có giá trị thì không đụng
        // (lát 1 là nguồn đáng tin nhất cho thông tin dự án).
        private static bool HasMissingScalars(BepParseResultDTO r) =>
            string.IsNullOrWhiteSpace(r.ProjectName)
            || string.IsNullOrWhiteSpace(r.ProjectDescription)
            || string.IsNullOrWhiteSpace(r.OwnerOrganizationName)
            || string.IsNullOrWhiteSpace(r.Address)
            || string.IsNullOrWhiteSpace(r.ContactAddress);
        // projectCode cố tình không tính: nó vắng mặt hợp lệ ở hầu hết BEP,
        // đưa vào sẽ khiến lượt chạy bù kích hoạt mọi lần.

        private static void FillMissingScalars(BepParseResultDTO target, BepParseResultDTO src)
        {
            target.ProjectName = Prefer(target.ProjectName, src.ProjectName);
            target.ProjectCode = Prefer(target.ProjectCode, src.ProjectCode);
            target.ProjectDescription = Prefer(target.ProjectDescription, src.ProjectDescription);
            target.OwnerOrganizationName = Prefer(target.OwnerOrganizationName, src.OwnerOrganizationName);
            target.Address = Prefer(target.Address, src.Address);
            target.ContactAddress = Prefer(target.ContactAddress, src.ContactAddress);
        }

        private static string? Prefer(string? current, string? candidate)
            => string.IsNullOrWhiteSpace(current) ? Blank(candidate) : current;

        // Prompt đã hai lần không chặn nổi việc model tách thành viên liên danh Chủ đầu tư ra
        // thành bên riêng. Lọc bằng code cho chắc: tên nhóm nằm gọn trong tên Chủ đầu tư
        // thì nhóm đó chính là Chủ đầu tư, không phải một bên độc lập trong CDE.
        private void DropOwnerAsGroup(BepParseResultDTO r)
        {
            var owner = NormalizeName(r.OwnerOrganizationName);

            // Chỉ lọc khi Chủ đầu tư thực sự là TÊN TỔ CHỨC. Nếu model điền một cụm vai trò ngắn
            // ("Project Owner", "KAITECH") thì phép chứa-trong ăn nhầm cả bên hợp lệ —
            // đo trên BEP KT-School: "Lead Appointed Party / Project Owner" bị loại oan.
            if (owner.Length < 15) return;

            int removed = r.Groups.RemoveAll(g =>
            {
                var name = NormalizeName(g.Name);
                // Ngưỡng độ dài để một tên chung chung ngắn không vô tình khớp.
                return name.Length >= 6 && owner.Contains(name, StringComparison.Ordinal);
            });

            if (removed > 0)
                _logger.LogInformation("Parse BEP: bỏ {Count} nhóm trùng với tên Chủ đầu tư", removed);
        }

        // Chức danh cá nhân, không phải tổ chức. Nhận diện theo TIỀN TỐ để "Bộ phận BIM",
        // "Ban Quản lý dự án 7" (đơn vị) vẫn sống, còn "Quản lý BIM", "Kỹ sư điện" (chức danh) thì rụng.
        private static readonly string[] PersonRolePrefixes =
        {
            "quan ly bim", "dieu phoi bim", "chu tri bim", "ky thuat vien", "chuyen vien",
            "ky su", "kien truc su", "giam sat vien", "nhan vien", "truong nhom", "quan tri vien",
            // BEP tiếng Anh dùng đúng các chức danh này (đo trên KT-School: "BIM Coordinators",
            // "BIM Modelers (Arch)"). Không thêm "architect"/"engineer" trần vì ở BEP tiếng Anh
            // chúng thường CHÍNH LÀ tên bên tham gia.
            "bim coordinator", "bim modeller", "bim modeler", "bim manager", "bim technician"
        };

        // Dấu hiệu tài liệu có giao trách nhiệm/quyền trong CDE cho chức danh đó.
        private static readonly string[] CdeResponsibilityHints =
        {
            "chịu trách nhiệm", "phụ trách", "được giao", "quyền", "cde",
            "phân công", "tham gia", "quản lý dữ liệu", "phối hợp"
        };

        private void DropPersonRoles(BepParseResultDTO r)
        {
            int removed = r.Groups.RemoveAll(g =>
            {
                var name = NormalizeName(g.Name);
                if (!PersonRolePrefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
                    return false;

                // Chức danh CHỈ bị loại khi tài liệu không mô tả trách nhiệm hay sự tham gia
                // nào trong CDE — có mô tả trách nhiệm thì nó là một nhóm làm việc hợp lệ.
                return string.IsNullOrWhiteSpace(g.Description)
                    || !CdeResponsibilityHints.Any(h => g.Description.Contains(h, StringComparison.OrdinalIgnoreCase));
            });

            if (removed > 0)
                _logger.LogInformation("Parse BEP: bỏ {Count} nhóm là chức danh cá nhân, không phải tổ chức", removed);
        }

        // Model có thể tự sinh trùng ngay trong MỘT response. MergeParts chỉ khử trùng lúc gộp lát,
        // nên tài liệu một lát không được lọc gì cả — đo trên BEP Viettel: "Bộ phận thiết kế kết cấu"
        // xuất hiện hai lần trong cùng một kết quả.
        private static void DedupeSelf(BepParseResultDTO r)
        {
            var groups = new List<BepGroupDTO>();
            foreach (var g in r.Groups)
            {
                var ex = groups.FirstOrDefault(x => SameName(x.Name, g.Name));
                if (ex is null) { groups.Add(g); continue; }

                if (Longer(g.Description, ex.Description)) ex.Description = g.Description;
                ex.PartnerOrganizationName ??= g.PartnerOrganizationName;
            }
            r.Groups = groups;

            var packages = new List<BepPackageDTO>();
            foreach (var p in r.Packages)
            {
                var ex = packages.FirstOrDefault(x => SameName(x.Name, p.Name));
                if (ex is null) { packages.Add(p); continue; }

                if (Longer(p.Description, ex.Description)) ex.Description = p.Description;
                ex.ContractValue ??= p.ContractValue;
                ex.Currency ??= p.Currency;
                ex.ContractorOrganizationName ??= p.ContractorOrganizationName;
            }
            r.Packages = packages;
        }

        // Prompt cấm rồi model vẫn tách thành viên liên danh (đo trên BEP 77 trang: liên danh tư vấn
        // bị tách thành 3 công ty riêng). Chặn bằng code: tên một nhóm nằm gọn trong tên của một
        // nhóm khác có chữ "liên danh" -> đó là thành viên, không phải một bên độc lập trong CDE.
        private void DropLienDanhMembers(BepParseResultDTO r)
        {
            var consortiums = r.Groups
                .Select(g => NormalizeName(g.Name))
                .Where(n => n.Contains("lien danh", StringComparison.Ordinal))
                .ToList();
            if (consortiums.Count == 0) return;

            int removed = r.Groups.RemoveAll(g =>
            {
                var name = NormalizeName(g.Name);
                return name.Length >= 10
                    && consortiums.Any(c => c != name && c.Contains(name, StringComparison.Ordinal));
            });

            if (removed > 0)
                _logger.LogInformation("Parse BEP: bỏ {Count} nhóm là thành viên của một liên danh đã có", removed);
        }

        // Ô tiêu đề/nhãn của bảng bị trích xuất lẻ ra thành nhóm: tên trần, chung chung, không mô tả.
        private static readonly string[] JunkGroupNames =
        {
            "bim", "thiet ke", "don vi", "to chuc", "ben", "nhom", "cde",
            "ghi chu", "mo ta", "stt", "vai tro", "ma hieu"
        };

        // Danh xưng tập thể chung chung — không trỏ tới bên nào cụ thể nên không cấp quyền được.
        // Loại BẤT KỂ có mô tả, vì model hay kèm mô tả kiểu "được nhắc đến chung chung".
        private static readonly string[] CollectiveGroupNames =
        {
            "cac ben", "cac ben lien quan", "ben lien quan", "cac don vi", "cac to chuc",
            "cac bo phan", "cac nhom", "cac ben tham gia", "cac doi tac"
        };

        // Mã viết tắt đơn vị trong quy ước đặt tên tệp (DP, HL, A2Z, HAD). Chúng rò rỉ vào groups
        // qua các ví dụ tên tệp nằm RẢI RÁC khắp tài liệu, không gom vào một mục nên bộ lọc theo
        // lát không bắt được. Dấu hiệu: một token viết hoa/số, rất ngắn, không có dấu cách.
        // Đo trên BEP 77 trang, mô tả model gán cho chúng còn sai sự thật ("DP - đơn vị chủ đầu tư"
        // trong khi DP là tư vấn thiết kế), nên giữ lại còn hại hơn bỏ sót.
        private static bool IsAbbreviationCode(string? name)
        {
            var n = name?.Trim();
            if (string.IsNullOrEmpty(n) || n.Length > 4 || n.Contains(' ')) return false;

            foreach (var c in n)
                if (!char.IsDigit(c) && !(char.IsLetter(c) && char.IsUpper(c))) return false;
            return true;
        }

        private void DropJunkGroups(BepParseResultDTO r)
        {
            int removed = r.Groups.RemoveAll(g =>
            {
                var name = NormalizeName(g.Name);

                if (IsAbbreviationCode(g.Name)) return true;
                if (CollectiveGroupNames.Contains(name)) return true;

                if (!string.IsNullOrWhiteSpace(g.Description)) return false;   // có mô tả -> giữ
                return name.Length < 5 || JunkGroupNames.Contains(name);
            });

            if (removed > 0)
                _logger.LogInformation("Parse BEP: bỏ {Count} nhóm rác (tên trần từ ô bảng)", removed);
        }

        private void DropUnsupportedPackages(BepParseResultDTO r)
        {
            int removed = r.Packages.RemoveAll(p =>
                p.ContractValue is null && string.IsNullOrWhiteSpace(p.ContractorOrganizationName));

            if (removed > 0)
                _logger.LogInformation("Parse BEP: bỏ {Count} gói thầu không có giá trị hợp đồng lẫn nhà thầu", removed);
        }

        private static bool Longer(string? candidate, string? current)
            => !string.IsNullOrWhiteSpace(candidate) && candidate.Length > (current?.Length ?? 0);

        private static bool SameName(string? a, string? b) => NormalizeName(a) == NormalizeName(b);
        private static string NormalizeName(string? s)
        {
            if (string.IsNullOrWhiteSpace(s)) return string.Empty;

            // Cắt chú thích trong ngoặc: "Tư vấn thiết kế (TVTK)" == "Tư vấn thiết kế".
            // Không phải khớp mờ — chỉ bỏ chú thích, nên không nuốt nhầm hai bên khác nhau.
            s = Regex.Replace(s, @"\([^)]*\)", " ");

            var decomposed = s.Trim().ToLowerInvariant().Replace('đ', 'd').Normalize(NormalizationForm.FormD);

            var sb = new StringBuilder(decomposed.Length);
            bool lastWasSpace = false;
            foreach (var ch in decomposed)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
                if (char.IsWhiteSpace(ch))
                {
                    if (!lastWasSpace) { sb.Append(' '); lastWasSpace = true; }
                    continue;
                }
                sb.Append(ch);
                lastWasSpace = false;
            }
            return sb.ToString().Trim();
        }

        // Bỏ các dòng ngắn lặp lại nhiều lần (header/footer trang, watermark) — giữ nội dung thật.
        // GENERIC: dựa trên tần suất lặp, không phụ thuộc nội dung/nhãn của bất kỳ file nào.
        private static string StripRepeatedLines(string text)
        {
            var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            int threshold = RepeatThreshold(text.Length);

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
                if (key.Length is > 3 and < 80 && freq.TryGetValue(key, out var c) && c >= threshold)
                    continue;
                sb.Append(l).Append('\n');
            }
            return sb.ToString();
        }


        private const int CharsPerPage = 3000;

        private static int RepeatThreshold(int totalChars) => Math.Max(5, totalChars / CharsPerPage / 2);

        // Schema hẹp cho pha 1a: chỉ 6 trường thông tin dự án, không có groups/packages.
        private static readonly object BepInfoSchema = new
        {
            type = "object",
            properties = new
            {
                projectName = new { type = "string" },
                projectCode = new { type = "string" },
                projectDescription = new { type = "string" },
                ownerOrganizationName = new { type = "string" },
                address = new { type = "string" },
                contactAddress = new { type = "string" }
            },
            required = new[]
            {
                "projectName", "projectCode", "projectDescription",
                "ownerOrganizationName", "address", "contactAddress"
            }
        };

        // Prompt pha 1a — CHỈ thông tin dự án. Tách khỏi phần quét nhóm vì nhồi cả hai vào một
        // lượt làm model chia trí: đo được trên BEP Viettel và Metropole, khi thêm hướng dẫn đọc
        // bảng phân quyền thì số nhóm đúng tăng mạnh nhưng address biến mất ở cả hai file.
        private static string BepInfoPrompt(string content) =>
            "Bạn là trợ lý trích xuất thông tin từ tài liệu BEP (BIM Execution Plan / Kế hoạch thực hiện BIM) để điền form khởi tạo dự án.\n" +
            "MỖI BEP CÓ CẤU TRÚC, CÁCH ĐÁNH MỤC VÀ ĐẶT NHÃN KHÁC NHAU. Hãy hiểu Ý NGHĨA nội dung để trích, KHÔNG phụ thuộc số mục hay nhãn cố định.\n\n" +

            "XỬ LÝ VĂN BẢN: text được trích tự động từ file (PDF/DOCX) nên nhiều từ có thể DÍNH LIỀN (mất dấu cách), và nội dung trong bảng bị trải thành từng dòng: nhãn ở một dòng, giá trị ở dòng ngay sau. Hãy tự tách từ, tự ghép nhãn với giá trị, và trả về giá trị chuẩn hoá (có dấu cách, đúng chính tả), GIỮ ĐÚNG nội dung gốc — không đổi tên riêng, không bịa.\n\n" +

            "TÀI LIỆU (phần đầu):\n" +
            $"{content}\n\n" +
            "TRÍCH ĐÚNG 6 TRƯỜNG SAU (theo Ý NGHĨA):\n" +
            "- projectName: tên dự án / công trình. Trường quan trọng nhất — hầu như BEP nào cũng có, cố gắng luôn tìm ra.\n" +
            "- projectCode: mã / ký hiệu dự án nếu có; không có để trống.\n" +
            "- projectDescription: 1-2 câu mô tả loại công trình và mục tiêu áp dụng BIM, dựa trên nội dung; không bịa số liệu.\n" +
            "- ownerOrganizationName: tên tổ chức Chủ đầu tư / Chủ sở hữu dự án (bên đặt hàng/thuê thực hiện).\n" +
            "- address: địa điểm / địa chỉ CÔNG TRÌNH (nơi thi công/xây dựng). Với công trình dạng TUYẾN (đường, cao tốc, cầu, hầm, kênh) thì địa điểm CHÍNH LÀ phạm vi tuyến: ghép 'Điểm đầu' và 'Điểm cuối' (kèm chiều dài tuyến nếu có) thành một chuỗi. Đừng bỏ trống chỉ vì tài liệu không ghi số nhà/đường.\n" +
            "- contactAddress: địa chỉ LIÊN HỆ / giao dịch (trụ sở, văn phòng chủ đầu tư, Ban QLDA, đầu mối liên hệ). Nếu tài liệu có một mục/ô mang nghĩa 'địa chỉ liên hệ' thì TRÍCH NGUYÊN VĂN giá trị đó, kể cả khi ghi ngắn gọn (chỉ quận/thành phố) hay không đầy đủ số nhà — vẫn tính là có. Chỉ để trống khi tài liệu KHÔNG hề nhắc tới địa chỉ liên hệ. Đây là trường KHÁC address: address = nơi xây dựng công trình, contactAddress = nơi liên hệ; không được điền trùng giá trị của address.\n\n" +

            "QUY TẮC:\n" +
            "- KHÔNG bịa. Trường không tìm thấy -> chuỗi rỗng.\n" +
            "- KHÔNG trích danh sách các bên tham gia ở lượt này — việc đó làm ở lượt khác. Chỉ tập trung đúng 6 trường trên.\n" +
            "- Bỏ qua bảng kỹ thuật thuần tuý (LOD, hệ toạ độ, quy ước đặt tên, danh sách phần mềm, ma trận phân quyền) vì chúng không chứa 6 trường trên.\n" +
            "- Chỉ trả JSON đúng schema, không kèm chữ nào khác.";

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
            "- address: địa điểm / địa chỉ CÔNG TRÌNH (nơi thi công/xây dựng). Với công trình dạng TUYẾN (đường, cao tốc, cầu, hầm, kênh) thì địa điểm CHÍNH LÀ phạm vi tuyến: ghép 'Điểm đầu' và 'Điểm cuối' (kèm chiều dài tuyến nếu có) thành một chuỗi. Đừng bỏ trống chỉ vì tài liệu không ghi số nhà/đường.\n" +
            "- contactAddress: địa chỉ LIÊN HỆ / giao dịch (trụ sở, văn phòng chủ đầu tư, Ban QLDA, đầu mối liên hệ). Nếu tài liệu có một mục/ô mang nghĩa 'địa chỉ liên hệ' thì TRÍCH NGUYÊN VĂN giá trị đó, kể cả khi ghi ngắn gọn (chỉ quận/thành phố) hay không đầy đủ số nhà — vẫn tính là có. Chỉ để trống khi tài liệu KHÔNG hề nhắc tới địa chỉ liên hệ. Đây là trường KHÁC address: address = nơi xây dựng công trình, contactAddress = nơi liên hệ; không được điền trùng giá trị của address.\n" +
            "- groups: DANH SÁCH CÁC BÊN THAM GIA / NHÓM LÀM VIỆC của dự án. Trích ĐẦY ĐỦ mọi bên tài liệu nêu — dù xuất hiện ở bảng phân công trách nhiệm, bảng phân quyền môi trường dữ liệu chung (CDE), sơ đồ tổ chức, hay bảng phối hợp. Các loại bên thường gặp (chỉ là gợi ý, không bắt buộc phải có): chủ đầu tư, tư vấn thiết kế (kiến trúc/kết cấu/MEP), nhà thầu thi công, tư vấn giám sát/thẩm tra, quản lý/điều phối BIM. Gộp bên trùng tên. Mỗi phần tử: name (tên bên), description (vai trò ngắn nếu tài liệu nêu), partnerOrganizationName (tên công ty cụ thể đảm nhận bên đó CHỈ khi tài liệu ghi rõ, không chắc để trống). Thông tin của nhóm tham gia nó là các bên làm việc bên trong 1 CDE, chứ ko phải tổng hợp thông tin các mục khác rồi suy diễn (thông tin các nhóm làm việc thường nằm trong mục lớn ' MÔI TRƯỜNG LÀM VIỆC CHUNG CDE' có thể là bảng với các phân quyền R, W, N). Lưu ý không được gộp các nhóm. Liệt kê trong file bao nhiêu nhóm thì sẽ để nguyên bấy nhiêu nhóm, ko gộp chung vài nhóm tương đồng (ví dụ như có nhiều bên tư vấn khác nhau (tư vấn MEP, tư vấn thiết kế) thì cũng tách ra) quyền hoặc trùng quyền hạn\n" +
            "- packages: gói thầu / hợp đồng — CHỈ tạo khi tài liệu nêu rõ gói thầu có tên (kèm giá trị hợp đồng, đơn vị tiền, nhà thầu nếu có). Nếu tài liệu chỉ mô tả phạm vi công việc/sản phẩm mà không có gói thầu -> MẢNG RỖNG. Không bịa gói thầu.\n\n" +

            "QUY TẮC:\n" +
            "- KHÔNG bịa. Trường không tìm thấy -> chuỗi rỗng; danh sách không có -> mảng rỗng.\n" +
            "- Bỏ qua các bảng kỹ thuật thuần tuý không chứa các trường trên (ví dụ bảng mức độ phát triển thông tin/LOD, hệ toạ độ, quy ước đặt tên, danh sách phần mềm).\n" +
            "- KHÔNG tách thành viên của BẤT KỲ LIÊN DANH nào thành các bên riêng — kể cả liên danh Chủ đầu tư, liên danh tư vấn hay liên danh nhà thầu. Một liên danh là MỘT bên: giữ nguyên tên đầy đủ của liên danh, không liệt kê từng công ty thành viên. Riêng liên danh Chủ đầu tư đã nằm ở ownerOrganizationName nên không lặp lại trong groups.\n" +
            "- KHÔNG lấy tên tổ chức từ trang bìa hay mục thông tin chung dự án để làm groups. Chỉ lấy các bên xuất hiện trong bảng phân công trách nhiệm, bảng phân quyền CDE, sơ đồ tổ chức hoặc bảng phối hợp.\n" +
            "- BẢNG MÃ VAI TRÒ DÙNG ĐỂ ĐẶT TÊN TỆP KHÔNG PHẢI CÁC BÊN THAM GIA. Dấu hiệu nhận biết: mỗi dòng là MỘT CHỮ CÁI (A, B, C, D, E, F, G, M, W, X…) kèm một chức danh chung chung (Kiến trúc sư, Kỹ sư điện, Nhà thầu, Nhà thầu phụ…). TUYỆT ĐỐI không đưa các dòng đó vào groups. Trường 'Originator' / 'Đơn vị khởi tạo' của quy ước đặt tên cũng vậy.\n" +
            "- MÃ VIẾT TẮT đơn vị (2-4 ký tự, ví dụ DP, HL, A2Z, HAD) dùng trong tên tệp KHÔNG phải một bên tham gia. Chỉ thấy mã mà không có tên đầy đủ của tổ chức thì BỎ QUA — tuyệt đối không tạo nhóm mà name chỉ là mã viết tắt.\n" +
            "- CHỨC DANH CỦA NGƯỜI KHÔNG PHẢI MỘT BÊN. 'Quản lý BIM (BIM Manager)', 'Điều phối BIM (BIM Coordinator)', 'Kỹ thuật viên BIM (BIM Modeller)', 'Kiến trúc sư', 'Kỹ sư điện'... là vai trò của cá nhân BÊN TRONG một tổ chức — TUYỆT ĐỐI không đưa vào groups. groups chỉ gồm TỔ CHỨC / ĐƠN VỊ: chủ đầu tư, ban quản lý dự án, công ty tư vấn, nhà thầu, hoặc bộ phận/phòng ban của một đơn vị (ví dụ 'Bộ phận BIM', 'Bộ phận Thiết kế' là hợp lệ vì là đơn vị, không phải chức danh).\n" +
            "- BẢNG PHÂN QUYỀN / MA TRẬN RACI thường bị trích xuất VỠ DỌC: tên các bên nằm ngay sau tiêu đề bảng, MỖI TỪ MỘT DÒNG và nối tiếp nhau (ví dụ: 'Chủ' / 'đầu tư' / 'Bộ' / 'phận' / 'BIM' / 'Bộ' / 'phận' / 'thiết' / 'kế' / 'Tư' / 'vấn' / 'thẩm' / 'tra'), rồi bên dưới mới tới các ô R, A, C, I. Hãy GHÉP các dòng rời đó lại thành tên từng bên. Đây là danh sách bên ĐÁNG TIN NHẤT của tài liệu — ưu tiên nó hơn mọi chỗ khác.\n" +
            "- TIÊU CHÍ ĐƯA VÀO groups: chỉ nhận bên mà tài liệu nêu rõ họ CHỊU TRÁCH NHIỆM hoặc THAM GIA trong môi trường dữ liệu chung (CDE) — có quyền R/W/N, được giao đầu việc, hoặc có mặt trong bảng phân công/phối hợp. Chức danh chỉ được nhắc trong sơ đồ tổ chức hay bảng chú giải vai trò mà KHÔNG kèm trách nhiệm hay quyền nào trong CDE thì KHÔNG đưa vào.\n" +
            "- partnerOrganizationName KHÔNG được trùng với name. Nếu name đã chính là tên công ty thì để partnerOrganizationName trống.\n" +
            "- Chỉ trả JSON đúng schema, không kèm chữ nào khác.";

        // Prompt 2 — chỉ quét nhóm/gói thầu trong MỘT lát giữa tài liệu.
        private static string BepPartsPrompt(string slice) =>
            "Đây là MỘT PHẦN của kế hoạch thực hiện BIM (BEP), không phải toàn bộ tài liệu.\n" +
            "Chỉ trích CÁC BÊN THAM GIA / NHÓM LÀM VIỆC và GÓI THẦU thực sự được nhắc tới trong đoạn dưới đây.\n\n" +

            "XỬ LÝ VĂN BẢN: text trích tự động từ file nên nhiều từ có thể DÍNH LIỀN và nội dung bảng bị trải thành từng dòng (nhãn một dòng, giá trị dòng sau). Tự tách từ, tự ghép nhãn với giá trị, giữ đúng nội dung gốc.\n\n" +

            "ĐOẠN TÀI LIỆU:\n" +
            $"{slice}\n\n" +

            "QUY TẮC:\n" +
            "- CHỈ lấy thứ xuất hiện trong đoạn trên. KHÔNG suy diễn, KHÔNG bổ sung các bên thông thường của ngành.\n" +
            "- Chỉ lấy bên xuất hiện trong bảng phân công trách nhiệm, bảng phân quyền CDE dạng R/W/N, sơ đồ tổ chức hoặc bảng phối hợp.\n" +
            "- BẢNG MÃ VAI TRÒ DÙNG ĐỂ ĐẶT TÊN TỆP KHÔNG PHẢI CÁC BÊN THAM GIA. Dấu hiệu: mỗi dòng là MỘT CHỮ CÁI (A, B, C, D, E, F, G, M, W, X…) kèm một chức danh chung chung (Kiến trúc sư, Kỹ sư điện, Nhà thầu…). TUYỆT ĐỐI không đưa vào groups. Trường 'Originator' / 'Đơn vị khởi tạo' cũng vậy.\n" +
            "- MÃ VIẾT TẮT đơn vị (2-4 ký tự như DP, HL, A2Z, HAD) trong tên tệp KHÔNG phải một bên. Chỉ có mã, không có tên đầy đủ tổ chức -> BỎ QUA.\n" +
            "- KHÔNG gộp các bên tương đồng: tài liệu liệt kê bao nhiêu bên thì giữ nguyên bấy nhiêu (nhiều loại tư vấn khác nhau thì tách riêng).\n" +
            "- KHÔNG tách thành viên của BẤT KỲ LIÊN DANH nào thành các bên riêng (liên danh Chủ đầu tư, liên danh tư vấn, liên danh nhà thầu). Một liên danh là MỘT bên, giữ nguyên tên đầy đủ.\n" +
            "- CHỨC DANH CỦA NGƯỜI KHÔNG PHẢI MỘT BÊN: 'Quản lý BIM', 'Điều phối BIM', 'Kỹ thuật viên BIM', 'Kiến trúc sư', 'Kỹ sư...' là vai trò của cá nhân trong một tổ chức — không đưa vào groups. Chỉ lấy TỔ CHỨC / ĐƠN VỊ / bộ phận.\n" +
            "- TIÊU CHÍ ĐƯA VÀO groups: chỉ nhận bên mà đoạn này nêu rõ họ CHỊU TRÁCH NHIỆM hoặc THAM GIA trong CDE (quyền R/W/N, được giao đầu việc, có mặt trong bảng phân công/phối hợp). Chức danh chỉ được liệt kê suông, không kèm trách nhiệm hay quyền trong CDE -> KHÔNG đưa vào.\n" +
            "- partnerOrganizationName KHÔNG được trùng với name; name đã là tên công ty thì để trống.\n" +
            "- packages: CHỈ tạo khi đoạn này nêu rõ một gói thầu CÓ TÊN kèm giá trị hợp đồng hoặc nhà thầu cụ thể. Mô tả phạm vi công việc của một bên KHÔNG phải là gói thầu — không được đổi tên nó thành 'Gói thầu ...'. Không chắc -> MẢNG RỖNG.\n" +
            "- Đoạn này không nhắc tới bên hay gói thầu nào -> trả về hai mảng rỗng.\n" +
            "- Bỏ qua bảng kỹ thuật thuần tuý (LOD, hệ toạ độ, quy ước đặt tên, danh sách phần mềm).\n" +
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

        // Schema hẹp cho pha 2: bỏ hết trường thông tin dự án vì chúng chỉ có ở phần đầu tài liệu.
        private static readonly object BepPartsSchema = new
        {
            type = "object",
            properties = new
            {
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
            required = new[] { "groups", "packages" }
        };

        // Thứ tự property = thứ tự SINH của structured output. Cố ý đặt evidence/suspicious TRƯỚC
        // summary: bản cũ sinh summary trước nên model tự khoá mình vào một kết luận, rồi buộc phải
        // chọn suspicious sao cho NHẤT QUÁN với cái tóm tắt vừa bịa -> cờ nghi ngờ không bao giờ bật.
        // summary KHÔNG nằm trong required: bắt buộc phải có = ép model bịa khi tệp rỗng nghĩa.
        private static readonly object AnalyzeSchema = new
        {
            type = "object",
            properties = new
            {
                evidence = new { type = "array", items = new { type = "string" } },
                contentTopic = new { type = "string" },
                suspicious = new { type = "boolean" },
                reason = new { type = "string" },
                summary = new { type = "string" }
            },
            required = new[] { "evidence", "contentTopic", "suspicious" }
        };

        private static string AnalyzeContentPrompt(string fileName, string? projectName,
                                                   string? projectDescription, string? folderName, string content) =>
            "Bạn kiểm tra một tệp vừa được tải lên hệ thống quản lý tài liệu xây dựng (CDE).\n" +
            "NGUYÊN TẮC BẮT BUỘC: mọi câu trả lời phải dựa DUY NHẤT vào phần NỘI DUNG TỆP bên dưới. " +
            "Phần bối cảnh dự án chỉ dùng để ĐỐI CHIẾU, TUYỆT ĐỐI không được dùng làm nguồn cho tóm tắt. " +
            "Nội dung tệp không nói gì thì tóm tắt phải để TRỐNG — không được suy ra từ tên dự án, mô tả dự án hay tên thư mục.\n\n" +

            // Nội dung đặt TRƯỚC bối cảnh: bản cũ đặt tên/mô tả dự án ngay trên content, nên khi
            // content nghèo chữ thì thứ gần nhất model bám vào để sinh tóm tắt là mô tả dự án.
            "===== NỘI DUNG TỆP (nguồn duy nhất) =====\n" +
            $"{content}\n" +
            "===== HẾT NỘI DUNG TỆP =====\n\n" +

            "BỐI CẢNH (chỉ để đối chiếu, KHÔNG phải nội dung tệp):\n" +
            $"- Tên tệp: {fileName}\n" +
            $"- Thư mục: {folderName}\n" +
            $"- Dự án: {projectName}\n" +
            $"- Mô tả dự án: {projectDescription}\n\n" +

            "Text được trích tự động từ PDF/DOCX nên có thể dính chữ, mất dấu cách, vỡ bảng — ĐÓ LÀ LỖI TRÍCH XUẤT, không phải dấu hiệu nghi ngờ.\n\n" +

            "Trả JSON theo ĐÚNG thứ tự các trường sau:\n" +
            "1) evidence: 2-4 cụm từ TRÍCH NGUYÊN VĂN từ NỘI DUNG TỆP (copy y hệt, không sửa, không dịch, mỗi cụm 3-10 từ). Đây là bằng chứng bạn đã thực sự đọc tệp. Tệp không đủ chữ -> mảng rỗng.\n" +
            "2) contentTopic: chủ đề THỰC TẾ của nội dung tệp, 5-15 từ, chỉ dựa trên evidence ở trên.\n" +
            "3) suspicious (boolean): true khi contentTopic KHÔNG phải tài liệu phục vụ dự án xây dựng/BIM (văn bản vu vơ, ghi chú cá nhân, nội dung thử nghiệm, truyện, hoá đơn, nội dung rác, lĩnh vực không liên quan như tuyển dụng/học tập), HOẶC lệch trắng trợn so với Tên tệp. Lỗi trích xuất, khác định dạng, nghi ngờ nhẹ -> false.\n" +
            "4) reason (TIẾNG VIỆT, 1 câu): CHỈ khi suspicious=true, nêu vì sao nghi ngờ; suspicious=false thì để trống.\n" +
            "5) summary (TIẾNG VIỆT, 1-3 câu, ~40 từ): loại tài liệu + chủ đề chính, CHỈ từ nội dung tệp. Bỏ mở đầu rườm rà ('Đây là', 'File này là'). KHÔNG nhận xét chất lượng. Nội dung tệp không đủ để tóm tắt -> để TRỐNG, tuyệt đối không lấy tên/mô tả dự án ra viết thay.\n" +
            "Chỉ trả JSON đúng schema, không kèm chữ nào khác.";

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            // num_ctx/num_gpu chưa cấu hình -> bỏ hẳn khỏi payload để Ollama dùng mặc định,
            // thay vì gửi null xuống làm hỏng options.
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };


        // Thứ tự các tham số phản chiếu thứ tự trong AnalyzeSchema — evidence trước, summary sau cùng.
        private record AnalysisJson(
            [property: JsonPropertyName("evidence")] List<string>? Evidence,
            [property: JsonPropertyName("contentTopic")] string? ContentTopic,
            [property: JsonPropertyName("suspicious")] bool Suspicious,
            [property: JsonPropertyName("reason")] string? Reason,
            [property: JsonPropertyName("summary")] string? Summary);

        private record GenerateRequest(string Model, string Prompt, bool Stream, bool Think, object Format, GenerateOptions Options);
        private record GenerateOptions(
            [property: JsonPropertyName("temperature")] double Temperature,
            [property: JsonPropertyName("num_predict")] int NumPredict,
            [property: JsonPropertyName("num_ctx")] int? NumCtx,
            [property: JsonPropertyName("num_gpu")] int? NumGpu);

        // done_reason/eval_count là thứ duy nhất cho biết vì sao output bị cắt.
        // Không map thì mọi lỗi đều rơi xuống JsonException mù ở chỗ Deserialize.
        private record GenerateResponse(
            string? Response,
            [property: JsonPropertyName("done_reason")] string? DoneReason,
            [property: JsonPropertyName("prompt_eval_count")] int? PromptEvalCount,
            [property: JsonPropertyName("eval_count")] int? EvalCount);
    }
}
