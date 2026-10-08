namespace Threads.Domain.Models.Versions;

public sealed class PostVersionSnapshot
{
    public string? Content { get; init; }

    public List<Guid> MediaIds { get; init; } = [];

    public PollVersionSnapshot? Poll { get; init; }

    public LocationVersionSnapshot? Location { get; init; }

    public LinkPreviewVersionSnapshot? LinkPreview { get; init; }

    public QuoteVersionSnapshot? Quote { get; init; }

}