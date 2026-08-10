namespace Threads.Application.DTOs.Locations;

public class LocationRequest
{
    public string? Id { get; init; }

    public required string Name { get; init; }

    public string? Country { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
