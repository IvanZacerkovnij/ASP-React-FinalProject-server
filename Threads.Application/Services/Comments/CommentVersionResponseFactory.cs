using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Versions;
using Threads.Application.Services.LinkPreviews;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.Services.Comments;

public sealed class CommentVersionResponseFactory
{
    private const int CurrentSchemaVersion = 1;

    private readonly IVersionSnapshotSerializer _snapshotSerializer;
    private readonly UserResponseFactory _userResponseFactory;

    public CommentVersionResponseFactory(
        IVersionSnapshotSerializer snapshotSerializer,
        UserResponseFactory userResponseFactory)
    {
        _snapshotSerializer = snapshotSerializer;
        _userResponseFactory = userResponseFactory;
    }

    public CommentResponse Create(
        CommentSummaryReadModel currentComment,
        CommentVersion version,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse>? mediaById = null)
    {
        ArgumentNullException.ThrowIfNull(currentComment);
        ArgumentNullException.ThrowIfNull(version);

        var snapshot = DeserializeSnapshot(version);

        return Create(currentComment, version, snapshot, mediaById);
    }

    internal CommentResponse Create(
        CommentSummaryReadModel currentComment,
        CommentVersion version,
        CommentVersionSnapshot snapshot,
        IReadOnlyDictionary<Guid, MediaAttachmentResponse>? mediaById = null)
    {
        ArgumentNullException.ThrowIfNull(currentComment);
        ArgumentNullException.ThrowIfNull(version);
        ArgumentNullException.ThrowIfNull(snapshot);

        if (version.CommentId != currentComment.Id)
        {
            throw new ArgumentException(
                "The version does not belong to the specified comment.",
                nameof(version));
        }

        return new CommentResponse
        {
            Id = currentComment.Id,
            VersionId = version.Id,
            PostId = currentComment.PostId,
            ParentCommentId = currentComment.ParentCommentId,
            Content = snapshot.Content,
            Author = _userResponseFactory.CreateShort(currentComment.Author),
            Attachments = snapshot.MediaIds
                .Select((mediaId, sortOrder) => mediaById is not null &&
                                                mediaById.TryGetValue(mediaId, out var media)
                    ? CopyMedia(media, sortOrder)
                    : null)
                .Where(media => media is not null)
                .Select(media => media!)
                .ToList(),
            Poll = MapPoll(currentComment, snapshot.Poll),
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
            LikesCount = currentComment.LikesCount,
            IsLikedByCurrentUser = currentComment.IsLikedByCurrentUser,
            RepliesCount = currentComment.RepliesCount,
            IsBookmarkedByCurrentUser = currentComment.IsBookmarkedByCurrentUser,
            RepostsCount = currentComment.RepostsCount,
            IsRepostedByCurrentUser = currentComment.IsRepostedByCurrentUser,
            ViewsCount = currentComment.ViewsCount,
            ActionAt = null,
            CreatedAt = currentComment.CreatedAt,
            UpdatedAt = version.CreatedAt
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
        CommentSummaryReadModel currentComment,
        PollVersionSnapshot? snapshot)
    {
        if (snapshot is null)
        {
            return null;
        }

        var currentPoll = currentComment.Poll?.Id == snapshot.Id
            ? currentComment.Poll
            : null;
        var currentOptions = currentPoll?.Options.ToDictionary(option => option.Id)
            ?? new Dictionary<Guid, PostPollOptionSummaryReadModel>();
        var historicalOptionIds = snapshot.Options.Select(option => option.Id).ToHashSet();
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
            CommentId = currentComment.Id,
            EndsAt = snapshot.EndsAt,
            TotalVotes = options.Sum(option => option.VotesCount),
            HasVotedByCurrentUser = selectedOptionId.HasValue,
            SelectedOptionId = selectedOptionId,
            Options = options
        };
    }

    internal CommentVersionSnapshot DeserializeSnapshot(CommentVersion version)
    {
        return version.SchemaVersion switch
        {
            CurrentSchemaVersion => _snapshotSerializer.Deserialize<CommentVersionSnapshot>(
                version.SnapshotJson),
            _ => throw new InvalidOperationException(
                $"Unsupported comment version schema '{version.SchemaVersion}'.")
        };
    }
}
