using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Application.DTOs.ResponseDTOs.Viewer;
using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace Application.Services
{
    public class ViewerService : IViewerService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;

        private readonly string _clientId;
        private readonly string _clientSecret;
        private readonly string _bucketKey;

        private const string ViewerScope = "viewer:read";
        private const string InternalScope = "data:read data:write data:create bucket:create bucket:read";

        public ViewerService(HttpClient http, IConfiguration config, IMemoryCache cache)
        {
            _http = http;
            _cache = cache;
            _clientId = config["Aps:ClientId"] ?? string.Empty;
            _clientSecret = config["Aps:ClientSecret"] ?? string.Empty;
            _bucketKey = config["Aps:BucketKey"] ?? "capstone-cde-2026";
        }

        public async Task<ViewerTokenResponseDTO> GetViewerTokenAsync(CancellationToken ct = default)
        {
            var (token, expiresIn) = await GetTokenAsync(ViewerScope, ct);
            return new ViewerTokenResponseDTO { AccessToken = token, ExpiresIn = expiresIn };
        }

        public async Task<UploadModelResponseDTO> UploadAndTranslateAsync(
            Stream content, string fileName, CancellationToken ct = default)
        {
            var safeFileName = Path.GetFileName(fileName);
            var objectKey = $"{Guid.NewGuid():N}_{safeFileName}";
            await EnsureBucketAsync(ct);
            var objectId = await SignedUploadAsync(objectKey, content, ct);
            var urn = ToBase64Url(objectId);
            await StartTranslationAsync(urn, ct);
            return new UploadModelResponseDTO { Urn = urn, FileName = safeFileName };
        }

        public async Task<TranslationStatusResponseDTO> GetStatusAsync(string urn, CancellationToken ct = default)
        {
            var (token, _) = await GetTokenAsync(InternalScope, ct);
            using var req = new HttpRequestMessage(
                HttpMethod.Get, $"/modelderivative/v2/designdata/{urn}/manifest");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            using var res = await _http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.NotFound)
                return new TranslationStatusResponseDTO { Status = "pending", Progress = "0% complete" };

            await EnsureSuccessAsync(res, "Không lấy được manifest dịch.", ct);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var root = doc.RootElement;
            return new TranslationStatusResponseDTO
            {
                Status = root.GetProperty("status").GetString() ?? "pending",
                Progress = root.TryGetProperty("progress", out var p) ? p.GetString() ?? string.Empty : string.Empty,
            };
        }

        private async Task<(string token, int expiresIn)> GetTokenAsync(string scope, CancellationToken ct)
        {
            EnsureConfigured();

            var cacheKey = $"aps_token::{scope}";
            if (_cache.TryGetValue(cacheKey, out (string token, int expiresIn) cached))
                return cached;

            var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{_clientId}:{_clientSecret}"));
            using var req = new HttpRequestMessage(HttpMethod.Post, "/authentication/v2/token");
            req.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "client_credentials",
                ["scope"] = scope,
            });

            using var res = await _http.SendAsync(req, ct);
            await EnsureSuccessAsync(res, "Lấy APS token thất bại.", ct);

            using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync(ct));
            var token = doc.RootElement.GetProperty("access_token").GetString()!;
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();

            var result = (token, expiresIn);
            _cache.Set(cacheKey, result, TimeSpan.FromSeconds(Math.Max(30, expiresIn - 60)));
            return result;
        }

        private async Task EnsureBucketAsync(CancellationToken ct)
        {
            var (token, _) = await GetTokenAsync(InternalScope, ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/oss/v2/buckets");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var body = JsonSerializer.Serialize(new { bucketKey = _bucketKey, policyKey = "persistent" });
            req.Content = new StringContent(body, Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            if (res.StatusCode == HttpStatusCode.Conflict) return;
            await EnsureSuccessAsync(res, "Tạo bucket OSS thất bại.", ct);
        }

        private async Task<string> SignedUploadAsync(string objectKey, Stream content, CancellationToken ct)
        {
            var (token, _) = await GetTokenAsync(InternalScope, ct);
            var encodedKey = Uri.EscapeDataString(objectKey);
            var signUrl = $"/oss/v2/buckets/{_bucketKey}/objects/{encodedKey}/signeds3upload";

            string uploadKey, signedUrl;
            using (var signReq = new HttpRequestMessage(HttpMethod.Get, signUrl))
            {
                signReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                using var signRes = await _http.SendAsync(signReq, ct);
                await EnsureSuccessAsync(signRes, "Xin signed URL thất bại.", ct);
                using var doc = JsonDocument.Parse(await signRes.Content.ReadAsStringAsync(ct));
                uploadKey = doc.RootElement.GetProperty("uploadKey").GetString()!;
                signedUrl = doc.RootElement.GetProperty("urls")[0].GetString()!;
            }

            using (var put = new HttpRequestMessage(HttpMethod.Put, signedUrl))
            {
                put.Content = new StreamContent(content);
                using var putRes = await _http.SendAsync(put, ct);
                await EnsureSuccessAsync(putRes, "Upload nội dung lên S3 thất bại.", ct);
            }

            using (var comp = new HttpRequestMessage(HttpMethod.Post, signUrl))
            {
                comp.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                comp.Content = new StringContent(
                    JsonSerializer.Serialize(new { uploadKey }), Encoding.UTF8, "application/json");
                using var compRes = await _http.SendAsync(comp, ct);
                await EnsureSuccessAsync(compRes, "Hoàn tất upload thất bại.", ct);
                using var doc = JsonDocument.Parse(await compRes.Content.ReadAsStringAsync(ct));
                return doc.RootElement.GetProperty("objectId").GetString()!;
            }
        }

        private async Task StartTranslationAsync(string urn, CancellationToken ct)
        {
            var (token, _) = await GetTokenAsync(InternalScope, ct);
            using var req = new HttpRequestMessage(HttpMethod.Post, "/modelderivative/v2/designdata/job");
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.Add("x-ads-force", "true");

            var body = new
            {
                input = new { urn },
                output = new { formats = new[] { new { type = "svf2", views = new[] { "2d", "3d" } } } },
            };
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, ct);
            await EnsureSuccessAsync(res, "Khởi tạo job dịch SVF2 thất bại.", ct);
        }

        private static string ToBase64Url(string objectId) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(objectId))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private void EnsureConfigured()
        {
            if (string.IsNullOrWhiteSpace(_clientId) || string.IsNullOrWhiteSpace(_clientSecret))
                throw new ApiExceptionResponse(
                    "APS chưa được cấu hình: thiếu Aps:ClientId/Aps:ClientSecret.", 500);
        }

        private static async Task EnsureSuccessAsync(HttpResponseMessage res, string message, CancellationToken ct)
        {
            if (res.IsSuccessStatusCode) return;

            var status = (int)res.StatusCode;
            var body = await res.Content.ReadAsStringAsync(ct);
            var snippet = body.Length > 300 ? body[..300] : body;
            throw new ApiExceptionResponse($"{message} (APS HTTP {status}) {snippet}", status >= 500 ? 502 : 400);
        }
    }
}
