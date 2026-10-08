using Threads.Application.Interfaces.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.Services.Posts;

public sealed class PostVersionFactory
{
    private const int CurrentSchemaVersion = 1;

    private readonly IVersionSnapshotSerializer _snapshotSerializer;

    public PostVersionFactory(IVersionSnapshotSerializer snapshotSerializer)
    {
        _snapshotSerializer = snapshotSerializer;
    }

    public PostVersion Create(Post post)
    {
        ArgumentNullException.ThrowIfNull(post);

        var snapshot = new PostVersionSnapshot
        {
            Content = post.Content,
            MediaIds = post.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => media.Id)
                .ToList(),
            Location = CreateLocation(post),
            LinkPreview = CreateLinkPreview(post),
            Poll = CreatePoll(post.Poll),
            Quote = CreateQuote(post.Quote),
        };

        return new PostVersion
        {
            Id = post.CurrentVersionId,
            PostId = post.Id,
            Post = post,
            SchemaVersion = CurrentSchemaVersion,
            SnapshotJson = _snapshotSerializer.Serialize(snapshot)
        };
    }

    private static LocationVersionSnapshot? CreateLocation(Post post)
    {
        if (string.IsNullOrWhiteSpace(post.LocationName))
        {
            return null;
        }

        return new LocationVersionSnapshot
        {
            Id = post.LocationPlaceId,
            Name = post.LocationName,
            Country = post.LocationCountry,
            Latitude = post.LocationLatitude,
            Longitude = post.LocationLongitude,
        };
    }
    
    private static LinkPreviewVersionSnapshot? CreateLinkPreview(Post post)
    {
        if (string.IsNullOrWhiteSpace(post.EmbedUrl))
        {
            return null;
        }

        return new LinkPreviewVersionSnapshot
        {
            Url = post.EmbedUrl,
            Title = post.EmbedTitle,
            Description = post.EmbedDescription,
            ThumbnailUrl = post.EmbedThumbnailUrl
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
    
    private static QuoteVersionSnapshot? CreateQuote(PostQuote? quote)
    {
        if (quote is null)
        {
            return null;
        }

        return new QuoteVersionSnapshot
        {
            TargetType = quote.TargetType,
            TargetId = quote.TargetId,
            TargetVersionId = quote.TargetVersionId
        };
    }
}