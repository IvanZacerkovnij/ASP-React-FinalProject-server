namespace Threads.Domain.Models.Versions;

public sealed class CommentVersionSnapshot
{
    public required string Content { get; init; }

    public List<Guid> MediaIds { get; init; } = [];

    public PollVersionSnapshot? Poll { get; init; }

    public LocationVersionSnapshot? Location { get; init; }

    public LinkPreviewVersionSnapshot? LinkPreview { get; init; }
}
