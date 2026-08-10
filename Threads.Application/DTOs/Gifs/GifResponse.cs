namespace Threads.Application.DTOs.Gifs;

public class GifResponse
{
    public required string Id { get; init; }

    public required string Title { get; init; }

    public required string OriginalUrl { get; init; }

    public required string PreviewUrl { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }
}
