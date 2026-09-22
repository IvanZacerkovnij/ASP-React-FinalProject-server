using AutoMapper;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.Services.Posts;

public sealed class PostResponseFactory
{
    private readonly IObjectStorageService _objectStorageService;
    private readonly UserResponseFactory _userResponseFactory;
    private readonly IMapper _mapper;

    public PostResponseFactory(
        IObjectStorageService objectStorageService,
        UserResponseFactory userResponseFactory,
        IMapper mapper)
    {
        _objectStorageService = objectStorageService;
        _userResponseFactory = userResponseFactory;
        _mapper = mapper;
    }

    public PostResponse Create(
        Post post,
        Guid? currentUserId,
        int viewsCount,
        DateTimeOffset? actionAt = null)
    {
        var response = _mapper.Map<PostResponse>(post);

        return new PostResponse
        {
            Id = response.Id,
            Content = response.Content,
            Author = _userResponseFactory.CreateShort(post.Author),
            Media = post.Media
                .OrderBy(media => media.SortOrder)
                .Select(MapMedia)
                .ToList(),
            Poll = MapPoll(post.Poll, currentUserId),
            Location = MapLocation(
                post.LocationPlaceId,
                post.LocationName,
                post.LocationCountry,
                post.LocationLatitude,
                post.LocationLongitude),
            Embed = MapEmbed(
                post.EmbedUrl,
                post.EmbedTitle,
                post.EmbedDescription,
                post.EmbedThumbnailUrl),
            LikesCount = response.LikesCount,
            CommentsCount = response.CommentsCount,
            RepostsCount = response.RepostsCount,
            ViewsCount = viewsCount,
            BookmarksCount = response.BookmarksCount,
            IsLikedByCurrentUser = currentUserId.HasValue &&
                post.PostLikes.Any(like => like.UserId == currentUserId.Value),
            IsRepostedByCurrentUser = currentUserId.HasValue &&
                post.PostReposts.Any(repost => repost.UserId == currentUserId.Value),
            IsBookmarkedByCurrentUser = currentUserId.HasValue &&
                post.PostBookmarks.Any(bookmark => bookmark.UserId == currentUserId.Value),
            ActionAt = actionAt,
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt
        };
    }

    public PostResponse Create(PostSummaryReadModel post)
    {
        return new PostResponse
        {
            Id = post.Id,
            Content = post.Content ?? string.Empty,
            Author = _userResponseFactory.CreateShort(post.Author),
            Media = post.Media
                .OrderBy(media => media.SortOrder)
                .Select(MapMedia)
                .ToList(),
            Poll = MapPoll(post.Id, post.Poll),
            Location = MapLocation(
                post.LocationPlaceId,
                post.LocationName,
                post.LocationCountry,
                post.LocationLatitude,
                post.LocationLongitude),
            Embed = MapEmbed(
                post.EmbedUrl,
                post.EmbedTitle,
                post.EmbedDescription,
                post.EmbedThumbnailUrl),
            LikesCount = post.LikesCount,
            CommentsCount = post.CommentsCount,
            RepostsCount = post.RepostsCount,
            BookmarksCount = post.BookmarksCount,
            ViewsCount = post.ViewsCount,
            IsLikedByCurrentUser = post.IsLikedByCurrentUser,
            IsRepostedByCurrentUser = post.IsRepostedByCurrentUser,
            IsBookmarkedByCurrentUser = post.IsBookmarkedByCurrentUser,
            ActionAt = post.ActionAt,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt
        };
    }

    public PostResponse Create(
        PostContentReadModel post,
        PostEngagementReadModel engagement,
        UserResponse author)
    {
        return new PostResponse
        {
            Id = post.Id,
            Content = post.Content ?? string.Empty,
            Author = MapUser(author),
            Media = post.Media
                .OrderBy(media => media.SortOrder)
                .Select(MapMedia)
                .ToList(),
            Poll = MapPoll(post, engagement),
            Location = MapLocation(
                post.LocationPlaceId,
                post.LocationName,
                post.LocationCountry,
                post.LocationLatitude,
                post.LocationLongitude),
            Embed = MapEmbed(
                post.EmbedUrl,
                post.EmbedTitle,
                post.EmbedDescription,
                post.EmbedThumbnailUrl),
            LikesCount = engagement.LikesCount,
            CommentsCount = engagement.CommentsCount,
            RepostsCount = engagement.RepostsCount,
            BookmarksCount = engagement.BookmarksCount,
            ViewsCount = engagement.ViewsCount,
            IsLikedByCurrentUser = engagement.IsLikedByCurrentUser,
            IsRepostedByCurrentUser = engagement.IsRepostedByCurrentUser,
            IsBookmarkedByCurrentUser = engagement.IsBookmarkedByCurrentUser,
            ActionAt = null,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt
        };
    }

    private PostMediaResponse MapMedia(MediaEntity media)
    {
        return new PostMediaResponse
        {
            Id = media.Id,
            Type = ResolveMediaResponseType(media.ContentType, media.Type),
            Url = _objectStorageService.GetReadUrl(media.StorageKey),
            ThumbnailUrl = ResolveMediaThumbnailUrl(
                media.StorageKey,
                media.ThumbnailStorageKey,
                media.ContentType,
                media.Type),
            Width = media.Width,
            Height = media.Height,
            Duration = media.DurationSeconds,
            MimeType = media.ContentType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = media.SortOrder
        };
    }

    private PostMediaResponse MapMedia(PostMediaReadModel media)
    {
        return new PostMediaResponse
        {
            Id = media.Id,
            Type = ResolveMediaResponseType(media.ContentType, media.Type),
            Url = _objectStorageService.GetReadUrl(media.StorageKey),
            ThumbnailUrl = ResolveMediaThumbnailUrl(
                media.StorageKey,
                media.ThumbnailStorageKey,
                media.ContentType,
                media.Type),
            Width = media.Width,
            Height = media.Height,
            Duration = media.DurationSeconds,
            MimeType = media.ContentType,
            FileName = media.FileName,
            SizeInBytes = media.SizeInBytes,
            SortOrder = media.SortOrder
        };
    }

    private static UserShortResponse MapUser(UserResponse user)
    {
        return new UserShortResponse
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Location = user.Location,
            AvatarUrl = user.AvatarUrl,
            IsVerified = user.IsVerified
        };
    }

    private static PollResponse? MapPoll(Poll? poll, Guid? currentUserId)
    {
        if (poll is null)
        {
            return null;
        }

        var currentVote = currentUserId.HasValue
            ? poll.Votes.FirstOrDefault(vote => vote.UserId == currentUserId.Value)
            : null;

        return new PollResponse
        {
            Id = poll.Id,
            PostId = poll.PostId,
            EndsAt = poll.EndsAt,
            TotalVotes = poll.Votes.Count,
            HasVotedByCurrentUser = currentVote is not null,
            SelectedOptionId = currentVote?.PollOptionId,
            Options = poll.Options
                .OrderBy(option => option.Position)
                .Select(option => new PollOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    Position = option.Position,
                    VotesCount = option.Votes.Count
                })
                .ToList()
        };
    }

    private static PollResponse? MapPoll(
        PostContentReadModel post,
        PostEngagementReadModel engagement)
    {
        if (post.Poll is null)
        {
            return null;
        }

        var pollEngagement = engagement.Poll;
        var optionEngagement = pollEngagement?.Options.ToDictionary(option => option.Id)
            ?? new Dictionary<Guid, PostPollOptionEngagementReadModel>();

        return new PollResponse
        {
            Id = post.Poll.Id,
            PostId = post.Id,
            EndsAt = post.Poll.EndsAt,
            TotalVotes = pollEngagement?.TotalVotes ?? 0,
            HasVotedByCurrentUser = pollEngagement?.SelectedOptionId.HasValue == true,
            SelectedOptionId = pollEngagement?.SelectedOptionId,
            Options = post.Poll.Options
                .OrderBy(option => option.Position)
                .Select(option => new PollOptionResponse
                {
                    Id = option.Id,
                    Text = option.Text,
                    Position = option.Position,
                    VotesCount = optionEngagement.TryGetValue(option.Id, out var optionEngagementState)
                        ? optionEngagementState.VotesCount
                        : 0
                })
                .ToList()
        };
    }

    private static PollResponse? MapPoll(Guid postId, PostPollSummaryReadModel? poll)
    {
        if (poll is null)
        {
            return null;
        }

        return new PollResponse
        {
            Id = poll.Id,
            PostId = postId,
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

    private static PostLocationResponse? MapLocation(
        string? id,
        string? name,
        string? country,
        double? latitude,
        double? longitude)
    {
        return string.IsNullOrWhiteSpace(name)
            ? null
            : new PostLocationResponse
            {
                Id = id,
                Name = name,
                Country = country,
                Latitude = latitude,
                Longitude = longitude
            };
    }

    private static PostEmbedResponse? MapEmbed(
        string? url,
        string? title,
        string? description,
        string? thumbnailUrl)
    {
        return string.IsNullOrWhiteSpace(url)
            ? null
            : new PostEmbedResponse
            {
                Url = url,
                Title = title,
                Description = description,
                ThumbnailUrl = thumbnailUrl
            };
    }

    private static string ResolveMediaResponseType(string contentType, MediaType mediaType)
    {
        if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return "gif";
        }

        return mediaType == MediaType.Video
            ? "video"
            : "image";
    }

    private string? ResolveMediaThumbnailUrl(
        string storageKey,
        string? thumbnailStorageKey,
        string contentType,
        MediaType mediaType)
    {
        if (!string.IsNullOrWhiteSpace(thumbnailStorageKey))
        {
            return _objectStorageService.GetReadUrl(thumbnailStorageKey);
        }

        var responseType = ResolveMediaResponseType(contentType, mediaType);

        return responseType is "image" or "gif"
            ? _objectStorageService.GetReadUrl(storageKey)
            : null;
    }
}
