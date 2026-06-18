using Amazon.Runtime;
using Microsoft.Extensions.Logging;

namespace Application.Services
{
    // Log request đã ký (Authorization/SignedHeaders + các header x-amz-*) và body lỗi từ Cloudian.
    // Body lỗi S3/Cloudian thường chứa <CanonicalRequest>/<StringToSign> -> so để biết field nào lệch chữ ký.
    internal sealed class S3DiagnosticsHandler : DelegatingHandler
    {
        private readonly ILogger _logger;
        public S3DiagnosticsHandler(ILogger logger) => _logger = logger;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var response = await base.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return response;

            var reqHeaders = string.Join("\n", request.Headers.Select(h => $"  {h.Key}: {string.Join(", ", h.Value)}"));
            var contentHeaders = request.Content?.Headers is { } ch
                ? string.Join("\n", ch.Select(h => $"  {h.Key}: {string.Join(", ", h.Value)}"))
                : "(none)";

            string body = string.Empty;
            if (response.Content != null)
            {
                body = await response.Content.ReadAsStringAsync(ct);
                // Dựng lại Content để AWS SDK vẫn đọc/parse được body sau khi ta đã đọc.
                var rebuilt = new StringContent(body);
                rebuilt.Headers.Clear();
                foreach (var h in response.Content.Headers)
                    rebuilt.Headers.TryAddWithoutValidation(h.Key, h.Value);
                response.Content = rebuilt;
            }

            _logger.LogWarning(
                "=== S3 ERROR {Status} ===\n{Method} {Uri}\n--- Request headers ---\n{ReqHeaders}\n--- Content headers ---\n{ContentHeaders}\n--- Response body ---\n{Body}\n=== END S3 ERROR ===",
                (int)response.StatusCode, request.Method, request.RequestUri, reqHeaders, contentHeaders, body);

            return response;
        }
    }

    // Bơm S3DiagnosticsHandler vào pipeline HTTP của AmazonS3Client. Dùng 1 HttpClient tái sử dụng.
    public sealed class S3DiagnosticHttpClientFactory : HttpClientFactory
    {
        private readonly ILogger _logger;
        private HttpClient? _http;

        public S3DiagnosticHttpClientFactory(ILogger logger) => _logger = logger;

        public override HttpClient CreateHttpClient(IClientConfig clientConfig)
            => _http ??= new HttpClient(new S3DiagnosticsHandler(_logger) { InnerHandler = new SocketsHttpHandler() });

        // Cho SDK cache 1 client theo chuỗi cố định (tránh tạo mới mỗi request).
        public override string GetConfigUniqueString(IClientConfig clientConfig) => "s3-diagnostics";

        public override bool UseSDKHttpClientCaching(IClientConfig clientConfig) => false;

        public override bool DisposeHttpClientsAfterUse(IClientConfig clientConfig) => false;
    }
}
