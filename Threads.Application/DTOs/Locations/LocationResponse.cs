namespace Threads.Application.DTOs.Locations;

public class LocationResponse
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Country { get; init; }

    public double Latitude { get; init; }

    public double Longitude { get; init; }
}
