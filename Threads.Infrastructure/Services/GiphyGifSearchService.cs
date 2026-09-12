using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
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

    public GiphyGifSearchService(
        HttpClient httpClient,
        IConfiguration configuration,
        HybridCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
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

                try
                {
                    using var response = await _httpClient.GetAsync(requestUri, token);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new ExternalServiceException(
                            $"GIPHY returned status code {(int)response.StatusCode}.");
                    }

                    var payload = await response.Content.ReadFromJsonAsync<GiphySearchResponse>(
                        cancellationToken: token);

                    return payload?.Data?
                        .Where(item =>
                            !string.IsNullOrWhiteSpace(item.Id) &&
                            !string.IsNullOrWhiteSpace(item.Images?.Original?.Url))
                        .Select(MapGifResponse)
                        .ToList() ?? [];
                }
                catch (HttpRequestException exception)
                {
                    throw new ExternalServiceException("Unable to communicate with GIPHY.", exception);
                }
                catch (JsonException exception)
                {
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
