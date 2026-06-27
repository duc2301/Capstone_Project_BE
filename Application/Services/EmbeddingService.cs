using Application.ExceptionMiddleware;
using Application.Interfaces.IServices;
using Application.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Application.Services
{
    public class EmbeddingService : IEmbeddingService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IOptions<OllamaOptions> _options;


        public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
        {
            var client = _httpClientFactory.CreateClient();
            client.Timeout = TimeSpan.FromMinutes(5);

            var url = $"{_options.Value.BaseUrl.TrimEnd('/')}/api/embeddings";
            var payload = new EmbedRequest(_options.Value.EmbeddingModel, text);

            var response = await client.PostAsync(url, new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json"), ct);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                throw new ApiExceptionResponse($"Ollama embedding lỗi: {body}", 502);
            }

            var result = await response.Content.ReadFromJsonAsync<EmbedResponse>(JsonOpts, ct);
            if (result?.Embedding is null || result.Embedding.Length != _options.Value.EmbeddingDimension)
                throw new ApiExceptionResponse(
                    $"Embedding sai chiều: mong đợi {_options.Value.EmbeddingDimension}, nhận {result?.Embedding?.Length ?? 0}.", 500);

            return result.Embedding;
        }

        public async Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
        {
            var list = new List<float[]>(texts.Count);
            foreach (var t in texts)
                list.Add(await EmbedAsync(t, ct));
            return list;
        }


        private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public EmbeddingService(IHttpClientFactory httpClientFactory, IOptions<OllamaOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _options = options;
        }


        // map sang JSON của Ollama
        private record EmbedRequest(string Model, string Prompt);
        private record EmbedResponse(float[]? Embedding);
    }
}

