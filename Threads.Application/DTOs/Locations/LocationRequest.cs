using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Locations;

public class LocationRequest
{
    [StringLength(1024, MinimumLength = 1)]
    public string? Id { get; init; }

    [Required, StringLength(255, MinimumLength = 1)]
    public required string Name { get; init; }

    [StringLength(255, MinimumLength = 1)]
    public string? Country { get; init; }

    [Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [Range(-180d, 180d)]
    public double? Longitude { get; init; }
}
