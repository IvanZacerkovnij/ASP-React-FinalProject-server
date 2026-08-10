using Threads.Domain.Common;
using Threads.Domain.Enums;

namespace Threads.Domain.Entities;

public class Media : BaseEntity
{
    public string StorageKey { get; set; } = null!;

    public string FileName { get; set; } = null!;

    public string ContentType { get; set; } = null!;

    public MediaType Type { get; set; }

    public long SizeInBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public double? DurationSeconds { get; set; }

    public string? ThumbnailStorageKey { get; set; }

    public int SortOrder { get; set; }

    public Guid UploadedByUserId { get; set; }

    public User UploadedByUser { get; set; } = null!;

    public Guid? PostId { get; set; }

    public Post? Post { get; set; }
}
