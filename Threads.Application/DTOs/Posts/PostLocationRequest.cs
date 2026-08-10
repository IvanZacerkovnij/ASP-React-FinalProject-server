namespace Threads.Application.DTOs.Posts;

public class PostLocationRequest
{
    public required string Name { get; init; }

    public double? Latitude { get; init; }

    public double? Longitude { get; init; }
}
