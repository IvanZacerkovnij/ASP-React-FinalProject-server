using Threads.Application.Interfaces.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.Services.Comments;

public sealed class CommentVersionFactory
{
    private const int CurrentSchemaVersion = 1;

    private readonly IVersionSnapshotSerializer _snapshotSerializer;

    public CommentVersionFactory(
        IVersionSnapshotSerializer snapshotSerializer)
    {
        _snapshotSerializer = snapshotSerializer;
    }

    public CommentVersion Create(Comment comment)
    {
        ArgumentNullException.ThrowIfNull(comment);

        var snapshot = new CommentVersionSnapshot
        {
            Content = comment.Content,
            MediaIds = comment.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => media.Id)
                .ToList(),
            Poll = CreatePoll(comment.Poll),
            Location = CreateLocation(comment),
            LinkPreview = CreateLinkPreview(comment)
        };

        return new CommentVersion
        {
            Id = comment.CurrentVersionId,
            CommentId = comment.Id,
            Comment = comment,
            SchemaVersion = CurrentSchemaVersion,
            SnapshotJson = _snapshotSerializer.Serialize(snapshot)
        };
    }

    private static PollVersionSnapshot? CreatePoll(Poll? poll)
    {
        if (poll is null)
        {
            return null;
        }

        return new PollVersionSnapshot
        {
            Id = poll.Id,
            EndsAt = poll.EndsAt,
            Options = poll.Options
                .OrderBy(option => option.Position)
                .Select(option => new PollOptionVersionSnapshot
                {
                    Id = option.Id,
                    Text = option.Text,
                    Position = option.Position
                })
                .ToList()
        };
    }

    private static LocationVersionSnapshot? CreateLocation(Comment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.LocationName))
        {
            return null;
        }

        return new LocationVersionSnapshot
        {
            Id = comment.LocationPlaceId,
            Name = comment.LocationName,
            Country = comment.LocationCountry,
            Latitude = comment.LocationLatitude,
            Longitude = comment.LocationLongitude
        };
    }

    private static LinkPreviewVersionSnapshot? CreateLinkPreview(Comment comment)
    {
        if (string.IsNullOrWhiteSpace(comment.LinkPreviewUrl))
        {
            return null;
        }

        return new LinkPreviewVersionSnapshot
        {
            Url = comment.LinkPreviewUrl,
            Title = comment.LinkPreviewTitle,
            ThumbnailUrl = comment.LinkPreviewImageUrl
        };
    }
}
