namespace Threads.Application.DTOs.Posts;

public class PostLocationResponse
{
    public string? Id { get; init; }

    public required string Name { get; init; }

    public string? Country { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
