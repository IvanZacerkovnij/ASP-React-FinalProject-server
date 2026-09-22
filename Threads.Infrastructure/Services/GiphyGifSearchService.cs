using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Threads.Application.DTOs.Gifs;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Gifs;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Services;

public class GiphyGifSearchService : IGifSearchService
{
    private const int DefaultLimit = 20;
    private const int MaxQueryLength = 50;
    private const string DefaultRating = "pg-13";

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly HybridCache _cache;
    private readonly ILogger<GiphyGifSearchService> _logger;

    public GiphyGifSearchService(
        HttpClient httpClient,
        IConfiguration configuration,
        HybridCache cache,
        ILogger<GiphyGifSearchService> logger)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
        _logger = logger;
    }

    public async Task<IReadOnlyCollection<GifResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim();

        if (normalizedQuery.Length > MaxQueryLength)
        {
            throw new RequestValidationException(
                $"GIF search query must be {MaxQueryLength} characters or less.");
        }

        var rating = _configuration["GIPHY_RATING"];
        var effectiveRating = string.IsNullOrWhiteSpace(rating) ? DefaultRating : rating;
        var cacheKey = $"giphy:search:{effectiveRating}:{normalizedQuery.ToLowerInvariant()}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                var apiKey = _configuration["GIPHY_API_KEY"] ??
                             throw new InfrastructureConfigurationException("GIPHY_API_KEY");

                var requestUri =
                    $"v1/gifs/search?api_key={Uri.EscapeDataString(apiKey)}" +
                    $"&q={Uri.EscapeDataString(normalizedQuery)}" +
                    $"&limit={DefaultLimit}" +
                    $"&rating={Uri.EscapeDataString(effectiveRating)}";
                var stopwatch = Stopwatch.StartNew();

                try
                {
                    using var response = await _httpClient.GetAsync(requestUri, token);

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger.LogWarning(
                            "GIPHY request returned status code {StatusCode} after {ElapsedMilliseconds} ms",
                            (int)response.StatusCode,
                            stopwatch.ElapsedMilliseconds);
                        throw new ExternalServiceException(
                            $"GIPHY returned status code {(int)response.StatusCode}.");
                    }

                    var payload = await response.Content.ReadFromJsonAsync<GiphySearchResponse>(
                        cancellationToken: token);

                    var gifs = payload?.Data?
                        .Where(item =>
                            !string.IsNullOrWhiteSpace(item.Id) &&
                            !string.IsNullOrWhiteSpace(item.Images?.Original?.Url))
                        .Select(MapGifResponse)
                        .ToList() ?? [];

                    _logger.LogDebug(
                        "GIPHY request returned {ResultCount} results in {ElapsedMilliseconds} ms",
                        gifs.Count,
                        stopwatch.ElapsedMilliseconds);
                    return gifs;
                }
                catch (HttpRequestException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "GIPHY request failed after {ElapsedMilliseconds} ms",
                        stopwatch.ElapsedMilliseconds);
                    throw new ExternalServiceException("Unable to communicate with GIPHY.", exception);
                }
                catch (JsonException exception)
                {
                    _logger.LogWarning(
                        exception,
                        "GIPHY returned invalid JSON after {ElapsedMilliseconds} ms",
                        stopwatch.ElapsedMilliseconds);
                    throw new ExternalServiceException("GIPHY returned an invalid response.", exception);
                }
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: cancellationToken);
    }

    private static GifResponse MapGifResponse(GiphyGifItem item)
    {
        var original = item.Images!.Original!;
        var previewUrl = item.Images.PreviewGif?.Url;

        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            previewUrl = item.Images.FixedWidthSmall?.Url;
        }

        if (string.IsNullOrWhiteSpace(previewUrl))
        {
            previewUrl = original.Url!;
        }

        return new GifResponse
        {
            Id = item.Id!,
            Title = string.IsNullOrWhiteSpace(item.Title) ? "GIF" : item.Title,
            OriginalUrl = original.Url!,
            PreviewUrl = previewUrl,
            Width = ParseDimension(original.Width),
            Height = ParseDimension(original.Height)
        };
    }

    private static int ParseDimension(string? value)
    {
        return int.TryParse(value, out var parsedValue)
            ? parsedValue
            : 0;
    }

    private sealed class GiphySearchResponse
    {
        [JsonPropertyName("data")]
        public List<GiphyGifItem> Data { get; init; } = [];
    }

    private sealed class GiphyGifItem
    {
        [JsonPropertyName("id")]
        public string? Id { get; init; }

        [JsonPropertyName("title")]
        public string? Title { get; init; }

        [JsonPropertyName("images")]
        public GiphyImages? Images { get; init; }
    }

    private sealed class GiphyImages
    {
        [JsonPropertyName("original")]
        public GiphyImageRendition? Original { get; init; }

        [JsonPropertyName("preview_gif")]
        public GiphyImageRendition? PreviewGif { get; init; }

        [JsonPropertyName("fixed_width_small")]
        public GiphyImageRendition? FixedWidthSmall { get; init; }
    }

    private sealed class GiphyImageRendition
    {
        [JsonPropertyName("url")]
        public string? Url { get; init; }

        [JsonPropertyName("width")]
        public string? Width { get; init; }

        [JsonPropertyName("height")]
        public string? Height { get; init; }
    }
}
