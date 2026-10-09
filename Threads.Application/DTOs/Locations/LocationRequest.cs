using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Locations;

public class LocationRequest
{
    [StringLength(1024, MinimumLength = 1)]
    public string? Id { get; set; }

    [Required, StringLength(255, MinimumLength = 1)]
    public required string Name { get; set; }

    [StringLength(255, MinimumLength = 1)]
    public string? Country { get; set; }

    [Range(-90d, 90d)]
    public double? Latitude { get; set; }

    [Range(-180d, 180d)]
    public double? Longitude { get; set; }
}
