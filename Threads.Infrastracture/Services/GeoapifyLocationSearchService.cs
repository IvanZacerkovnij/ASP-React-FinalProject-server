using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Configuration;
using Threads.Application.DTOs.Locations;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Locations;
using Threads.Infrastracture.Exceptions;

namespace Threads.Infrastracture.Services;

public class GeoapifyLocationSearchService : ILocationSearchService
{
    private const int DefaultLimit = 10;
    private const int MaxQueryLength = 100;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;
    private readonly HybridCache _cache;

    public GeoapifyLocationSearchService(
        HttpClient httpClient,
        IConfiguration configuration,
        HybridCache cache)
    {
        _httpClient = httpClient;
        _configuration = configuration;
        _cache = cache;
    }

    public async Task<IReadOnlyCollection<LocationResponse>> SearchAsync(
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
                $"Location search query must be {MaxQueryLength} characters or less.");
        }

        var cacheKey = $"geoapify:search:{normalizedQuery.ToLowerInvariant()}";

        return await _cache.GetOrCreateAsync(
            cacheKey,
            async token =>
            {
                var apiKey = _configuration["GEOAPIFY_API_KEY"] ??
                             throw new InfrastructureConfigurationException("GEOAPIFY_API_KEY");

                var requestUri =
                    $"v1/geocode/autocomplete?text={Uri.EscapeDataString(normalizedQuery)}" +
                    $"&format=json" +
                    $"&limit={DefaultLimit}" +
                    $"&apiKey={Uri.EscapeDataString(apiKey)}";

                try
                {
                    using var response = await _httpClient.GetAsync(requestUri, token);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new ExternalServiceException(
                            $"Geoapify returned status code {(int)response.StatusCode}.");
                    }

                    var payload = await response.Content.ReadFromJsonAsync<GeoapifyAutocompleteResponse>(
                        cancellationToken: token);

                    return payload?.Results?
                        .Where(item => item.Latitude.HasValue && item.Longitude.HasValue)
                        .Select(MapLocationResponse)
                        .ToList() ?? [];
                }
                catch (HttpRequestException exception)
                {
                    throw new ExternalServiceException("Unable to communicate with Geoapify.", exception);
                }
                catch (JsonException exception)
                {
                    throw new ExternalServiceException("Geoapify returned an invalid response.", exception);
                }
            },
            new HybridCacheEntryOptions
            {
                Expiration = TimeSpan.FromMinutes(30),
                LocalCacheExpiration = TimeSpan.FromMinutes(5)
            },
            cancellationToken: cancellationToken);
    }

    private static LocationResponse MapLocationResponse(GeoapifyLocationItem item)
    {
        var country = string.IsNullOrWhiteSpace(item.Country)
            ? "Unknown country"
            : item.Country;

        var name = item.Formatted;

        if (string.IsNullOrWhiteSpace(name))
        {
            name = item.Name;
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            name = country;
        }

        var id = !string.IsNullOrWhiteSpace(item.PlaceId)
            ? item.PlaceId
            : $"{item.Latitude}:{item.Longitude}:{name}";

        return new LocationResponse
        {
            Id = id,
            Name = name,
            Country = country,
            Latitude = item.Latitude!.Value,
            Longitude = item.Longitude!.Value
        };
    }

    private sealed class GeoapifyAutocompleteResponse
    {
        [JsonPropertyName("results")]
        public List<GeoapifyLocationItem> Results { get; init; } = [];
    }

    private sealed class GeoapifyLocationItem
    {
        [JsonPropertyName("place_id")]
        public string? PlaceId { get; init; }

        [JsonPropertyName("name")]
        public string? Name { get; init; }

        [JsonPropertyName("formatted")]
        public string? Formatted { get; init; }

        [JsonPropertyName("country")]
        public string? Country { get; init; }

        [JsonPropertyName("lat")]
        public double? Latitude { get; init; }

        [JsonPropertyName("lon")]
        public double? Longitude { get; init; }
    }
}
