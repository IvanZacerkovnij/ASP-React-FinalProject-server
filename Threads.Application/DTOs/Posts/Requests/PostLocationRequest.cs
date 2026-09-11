using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace Threads.Application.DTOs.Posts.Requests;

public class PostLocationRequest
{
    [StringLength(1024, MinimumLength = 1)]
    public string? Id { get; init; }

    [Required, StringLength(255, MinimumLength = 1)]
    public required string Name { get; init; }

    [DefaultValue(null), StringLength(255, MinimumLength = 1)]
    public string? Country { get; init; }

    [DefaultValue(null), Range(-90d, 90d)]
    public double? Latitude { get; init; }

    [DefaultValue(null), Range(-180d, 180d)]
    public double? Longitude { get; init; }
}
