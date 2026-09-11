namespace Threads.Application.DTOs.Posts.Requests;

public class PostLocationRequest
{
    public string? Id { get; init; }

    public required string Name { get; init; }

    public string? Country { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
