using Threads.Domain.Common;

namespace Threads.Domain.Entities;

public class ScheduledPost : BaseEntity
{
    public uint Version { get; set; }

    public string? Content { get; set; }

    public string? LinkPreviewUrl { get; set; }

    public string? LinkPreviewTitle { get; set; }

    public string? LinkPreviewImageUrl { get; set; }

    public DateTimeOffset ScheduledAt { get; set; }

    public Guid AuthorId { get; set; }

    public User Author { get; set; } = null!;

    public ICollection<Media> Media { get; set; } =
        new List<Media>();
}
