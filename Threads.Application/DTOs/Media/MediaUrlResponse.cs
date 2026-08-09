namespace Threads.Application.DTOs.Media;

public class MediaUrlResponse
{
    public Guid Id { get; init; }

    public required string Url { get; init; }
}
