using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Quotes;
using Threads.Application.Interfaces.Versions;
using Threads.Application.Services.LinkPreviews;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.Services.Posts;

public sealed class PostVersionResponseFactory
{
    private const int CurrentSchemaVersion = 1;

    private readonly IVersionSnapshotSerializer _snapshotSerializer;
    private readonly UserResponseFactory _userResponseFactory;

    public PostVersionResponseFactory(
        IVersionSnapshotSerializer snapshotSerializer,
        UserResponseFactory userResponseFactory)
    {
        _snapshotSerializer = snapshotSerializer;
        _userResponseFactory = userResponseFactory;
    }

    public PostResponse Create(
        PostSummaryReadModel currentPost,
        PostVersion version,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse> mediaById,
        QuoteResponse? resolvedQuote = null)
    {
        ArgumentNullException.ThrowIfNull(currentPost);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(mediaById);

        var snapshot = DeserializeSnapshot(version);

        return Create(currentPost, version, snapshot, mediaById, resolvedQuote);
    }

    internal PostResponse Create(
        PostSummaryReadModel currentPost,
        PostVersion version,
        PostVersionSnapshot snapshot,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse> mediaById,
        QuoteResponse? resolvedQuote = null)
    {
        ArgumentNullException.ThrowIfNull(currentPost);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(mediaById);

        if (version.PostId != currentPost.Id)
        {
            throw new ArgumentException(
                "The version does not belong to the specified post.",
                nameof(version));
        }

        return new PostResponse
        {
            Id = currentPost.Id,
            VersionId = version.Id,
            Content = snapshot.Content ?? string.Empty,
            Author = _userResponseFactory.CreateShort(currentPost.Author),
            Media = snapshot.MediaIds
                .Select((mediaId, sortOrder) => mediaById.TryGetValue(mediaId, out var media)
                    ? CopyMedia(media, sortOrder)
                    : null)
                .Where(media => media is not null)
                .Select(media => media!)
                .ToList(),
            Poll = MapPoll(currentPost, snapshot.Poll),
            Location = snapshot.Location is null
                ? null
                : new PostLocationResponse
                {
                    Id = snapshot.Location.Id,
                    Name = snapshot.Location.Name,
                    Country = snapshot.Location.Country,
                    Latitude = snapshot.Location.Latitude,
                    Longitude = snapshot.Location.Longitude
                },
            LinkPreview = snapshot.LinkPreview is null
                ? null
                : LinkPreviewResponseFactory.Create(
                    snapshot.LinkPreview.Url,
                    snapshot.LinkPreview.Title,
                    snapshot.LinkPreview.ThumbnailUrl),
            Quote = MapQuote(snapshot.Quote, resolvedQuote),
            LikesCount = currentPost.LikesCount,
            CommentsCount = currentPost.CommentsCount,
            RepostsCount = currentPost.RepostsCount,
            BookmarksCount = currentPost.BookmarksCount,
            ViewsCount = currentPost.ViewsCount,
            IsLikedByCurrentUser = currentPost.IsLikedByCurrentUser,
            IsRepostedByCurrentUser = currentPost.IsRepostedByCurrentUser,
            IsBookmarkedByCurrentUser = currentPost.IsBookmarkedByCurrentUser,
            ActionAt = null,
            CreatedAt = currentPost.CreatedAt,
            UpdatedAt = version.CreatedAt
        };
    }

    internal PostVersionSnapshot DeserializeSnapshot(PostVersion version)
    {
        return version.SchemaVersion switch
        {
            CurrentSchemaVersion => _snapshotSerializer.Deserialize<PostVersionSnapshot>(
                version.SnapshotJson),
            _ => throw new InvalidOperationException(
                $"Unsupported post version schema '{version.SchemaVersion}'.")
        };
    }

    private static MediaAttachmentResponse CopyMedia(
        MediaAttachmentResponse media,
        int sortOrder)
    {
        return new MediaAttachmentResponse
        {
            Id = media.Id,
            Type = media.Type,
            Url = media.Url,
            ThumbnailUrl = media.ThumbnailUrl,
            Width = media.Width,
            Height = media.Height,
            Duration = media.Duration,
            MimeType = media.MimeType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = sortOrder
        };
    }

    private static PollResponse? MapPoll(
        PostSummaryReadModel currentPost,
        PollVersionSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var currentPoll = currentPost.Poll?.Id == snapshot.Id
            ? currentPost.Poll
            : null;
        var currentOptions = currentPoll?.Options.ToDictionary(option => option.Id)
            ?? new Dictionary<Guid, PostPollOptionSummaryReadModel>();
        var historicalOptionIds = snapshot.Options
            .Select(option => option.Id)
            .ToHashSet();
        var selectedOptionId = currentPoll?.SelectedOptionId is Guid selectedId &&
                               historicalOptionIds.Contains(selectedId)
            ? selectedId
            : (Guid?)null;

        var options = snapshot.Options
            .OrderBy(option => option.Position)
            .Select(option => new PollOptionResponse
            {
                Id = option.Id,
                Text = option.Text,
                Position = option.Position,
                VotesCount = currentOptions.TryGetValue(option.Id, out var currentOption)
                    ? currentOption.VotesCount
                    : 0
            })
            .ToList();

        return new PollResponse
        {
            Id = snapshot.Id,
            PostId = currentPost.Id,
            EndsAt = snapshot.EndsAt,
            TotalVotes = options.Sum(option => option.VotesCount),
            HasVotedByCurrentUser = selectedOptionId.HasValue,
            SelectedOptionId = selectedOptionId,
            Options = options
        };
    }

    private static QuoteResponse? MapQuote(
        QuoteVersionSnapshot? snapshot,
        QuoteResponse? resolvedQuote)
    {
        if (snapshot is null)
        {
            return null;
        }

        var matchesSnapshot = resolvedQuote is not null &&
                              resolvedQuote.TargetType == snapshot.TargetType &&
                              resolvedQuote.TargetId == snapshot.TargetId &&
                              resolvedQuote.TargetVersionId == snapshot.TargetVersionId;

        return new QuoteResponse
        {
            TargetType = snapshot.TargetType,
            TargetId = snapshot.TargetId,
            TargetVersionId = snapshot.TargetVersionId,
            HasNewVersion = matchesSnapshot && resolvedQuote!.HasNewVersion,
            ReplyingToUsernames = matchesSnapshot
                ? resolvedQuote!.ReplyingToUsernames
                : [],
            Target = matchesSnapshot
                ? resolvedQuote!.Target
                : null
        };
    }
}
