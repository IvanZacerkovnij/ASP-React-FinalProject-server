using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Configuration;
using Threads.Application.DTOs.Locations;
using Threads.Application.Interfaces.Locations;

namespace Threads.Infrastracture.Services;

public class GeoapifyLocationSearchService : ILocationSearchService
{
    private const int DefaultLimit = 10;
    private const int MaxQueryLength = 100;

    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public GeoapifyLocationSearchService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _configuration = configuration;
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
            throw new ArgumentException(
                $"Location search query must be {MaxQueryLength} characters or less.",
                nameof(query));
        }

        var apiKey = _configuration["GEOAPIFY_API_KEY"];

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new InvalidOperationException("GEOAPIFY_API_KEY is not configured.");
        }

        var requestUri =
            $"v1/geocode/autocomplete?text={Uri.EscapeDataString(normalizedQuery)}" +
            $"&format=json" +
            $"&limit={DefaultLimit}" +
            $"&apiKey={Uri.EscapeDataString(apiKey)}";

        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Geoapify returned status code {(int)response.StatusCode}.",
                null,
                response.StatusCode);
        }

        var payload = await response.Content.ReadFromJsonAsync<GeoapifyAutocompleteResponse>(
            cancellationToken: cancellationToken);

        if (payload?.Results is null || payload.Results.Count == 0)
        {
            return [];
        }

        return payload.Results
            .Where(item => item.Latitude.HasValue && item.Longitude.HasValue)
            .Select(MapLocationResponse)
            .ToList();
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
