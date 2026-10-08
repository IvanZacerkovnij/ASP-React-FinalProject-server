using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.LinkPreviews;
using Threads.Application.Services.Users;
using Threads.Domain.Enums;

namespace Threads.Application.Services.Comments;

public sealed class CommentResponseFactory
{
    private readonly UserResponseFactory _userResponseFactory;
    private readonly IObjectStorageService _objectStorageService;

    public CommentResponseFactory(
        UserResponseFactory userResponseFactory,
        IObjectStorageService objectStorageService)
    {
        _userResponseFactory = userResponseFactory;
        _objectStorageService = objectStorageService;
    }

    public CommentResponse Create(CommentSummaryReadModel comment)
    {
        return new CommentResponse
        {
            Id = comment.Id,
            VersionId = comment.VersionId,
            PostId = comment.PostId,
            ParentCommentId = comment.ParentCommentId,
            Content = comment.Content,
            Author = _userResponseFactory.CreateShort(comment.Author),
            Attachments = comment.Media
                .OrderBy(media => media.SortOrder)
                .Select(MapMedia)
                .ToList(),
            Poll = MapPoll(comment.Id, comment.Poll),
            Location = MapLocation(comment),
            LinkPreview = string.IsNullOrWhiteSpace(comment.LinkPreviewUrl)
                ? null
                : LinkPreviewResponseFactory.Create(
                    comment.LinkPreviewUrl,
                    comment.LinkPreviewTitle,
                    comment.LinkPreviewImageUrl),
            LikesCount = comment.LikesCount,
            IsLikedByCurrentUser = comment.IsLikedByCurrentUser,
            RepliesCount = comment.RepliesCount,
            IsBookmarkedByCurrentUser = comment.IsBookmarkedByCurrentUser,
            RepostsCount = comment.RepostsCount,
            IsRepostedByCurrentUser = comment.IsRepostedByCurrentUser,
            ViewsCount = comment.ViewsCount,
            ActionAt = comment.ActionAt,
            CreatedAt = comment.CreatedAt,
            UpdatedAt = comment.UpdatedAt
        };
    }

    private MediaAttachmentResponse MapMedia(PostMediaReadModel media)
    {
        var type = ResolveMediaType(media.ContentType, media.Type);
        var url = _objectStorageService.GetReadUrl(media.StorageKey);

        return new MediaAttachmentResponse
        {
            Id = media.Id,
            Type = type,
            Url = url,
            ThumbnailUrl = !string.IsNullOrWhiteSpace(media.ThumbnailStorageKey)
                ? _objectStorageService.GetReadUrl(media.ThumbnailStorageKey)
                : type is "image" or "gif"
                    ? url
                    : null,
            Width = media.Width,
            Height = media.Height,
            Duration = media.DurationSeconds,
            MimeType = media.ContentType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = media.SortOrder
        };
    }

    private static PollResponse? MapPoll(Guid commentId, PostPollSummaryReadModel? poll)
    {
        if (poll is null)
        {
            return null;
        }

        return new PollResponse
        {
            Id = poll.Id,
            CommentId = commentId,
            EndsAt = poll.EndsAt,
            TotalVotes = poll.TotalVotes,
            HasVotedByCurrentUser = poll.SelectedOptionId.HasValue,
            SelectedOptionId = poll.SelectedOptionId,
            Options = poll.Options
                .OrderBy(option => option.Position)
                .Select(option => new PollOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    Position = option.Position,
                    VotesCount = option.VotesCount
                })
                .ToList()
        };
    }

    private static PostLocationResponse? MapLocation(CommentSummaryReadModel comment)
    {
        return string.IsNullOrWhiteSpace(comment.LocationName)
            ? null
            : new PostLocationResponse
            {
                Id = comment.LocationPlaceId,
                Name = comment.LocationName,
                Country = comment.LocationCountry,
                Latitude = comment.LocationLatitude,
                Longitude = comment.LocationLongitude
            };
    }

    private static string ResolveMediaType(string contentType, MediaType mediaType)
    {
        if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return "gif";
        }

        return mediaType == MediaType.Video
            ? "video"
            : "image";
    }
}
