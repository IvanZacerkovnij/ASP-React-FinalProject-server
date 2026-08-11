using AutoMapper;
using Threads.Application.DTOs.Locations;
using Threads.Application.DTOs.Polls;
using Threads.Application.DTOs.Posts;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.Services;

public class PostService : IPostService
{
    private const int FeedSize = 10;
    private const int MaxContentLength = 2000;
    private const int MaxLocationNameLength = 255;
    private const int MaxLocationCountryLength = 255;
    private const int MaxLocationIdLength = 1024;
    private const int MaxEmbedUrlLength = 2048;
    private const int MaxEmbedTitleLength = 255;
    private const int MaxEmbedDescriptionLength = 1000;
    private readonly IPostRepository _postRepository;
    private readonly IMediaRepository _mediaRepository;
    private readonly IObjectStorageService _objectStorageService;
    private readonly IMapper _mapper;

    public PostService(
        IPostRepository postRepository,
        IMediaRepository mediaRepository,
        IObjectStorageService objectStorageService,
        IMapper mapper)
    {
        _postRepository = postRepository;
        _mediaRepository = mediaRepository;
        _objectStorageService = objectStorageService;
        _mapper = mapper;
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetAllAsync(
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetAllAsync(cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetFeedAsync(
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetRandomAsync(FeedSize, cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetByAuthorIdAsync(
        Guid authorId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetByAuthorIdAsync(authorId, cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetLikedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetLikedByUserIdAsync(userId, cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> GetRepostedByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var posts = await _postRepository.GetRepostedByUserIdAsync(userId, cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<IReadOnlyCollection<PostResponse>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var posts = await _postRepository.SearchAsync(query.Trim(), cancellationToken: cancellationToken);

        return posts
            .Select(post => MapPostResponse(post, currentUserId))
            .ToList();
    }

    public async Task<PostResponse?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default,
        Guid? currentUserId = null)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        return post is null
            ? null
            : MapPostResponse(post, currentUserId);
    }

    public async Task<PostResponse> CreateAsync(
        Guid authorId,
        CreatePostRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidatePostCreateRequest(request);
        var mediaIds = request.MediaIds ?? [];

        var post = new Post
        {
            AuthorId = authorId,
            Content = NormalizeContent(request.Content),
            Poll = MapPoll(request.Poll)
        };

        ApplyLocation(post, request.Location);
        ApplyEmbed(post, request.Embed);

        await ApplyMediaAsync(post, authorId, mediaIds, cancellationToken);

        await _postRepository.AddAsync(post, cancellationToken);

        var createdPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);

        return MapPostResponse(createdPost ?? post, authorId);
    }

    public async Task<PostResponse?> UpdateAsync(Guid id, UpdatePostRequest request, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            return null;
        }

        if (request.Content is not null)
        {
            post.Content = NormalizeContent(request.Content);
        }

        if (request.MediaIds is not null)
        {
            await ApplyMediaAsync(post, post.AuthorId, request.MediaIds, cancellationToken);
        }

        if (request.RemoveLocation)
        {
            ClearLocation(post);
        }
        else if (request.Location is not null)
        {
            ApplyLocation(post, request.Location);
        }

        if (request.RemoveEmbed)
        {
            ClearEmbed(post);
        }
        else if (request.Embed is not null)
        {
            ApplyEmbed(post, request.Embed);
        }

        if (request.Poll is not null)
        {
            if (post.Poll is not null)
            {
                throw new InvalidOperationException("Updating an existing poll is not supported.");
            }

            post.Poll = MapPoll(request.Poll);
        }

        ValidatePostState(post);
        post.UpdatedAt = DateTimeOffset.UtcNow;

        await _postRepository.UpdateAsync(post, cancellationToken);

        var updatedPost = await _postRepository.GetByIdAsync(post.Id, cancellationToken);

        return MapPostResponse(updatedPost ?? post, post.AuthorId);
    }

    public async Task<PostViewResponse?> RecordViewAsync(
        Guid id,
        Guid viewerId,
        CancellationToken cancellationToken = default)
    {
        var viewsCount = await _postRepository.RecordViewAsync(id, viewerId, cancellationToken);

        return viewsCount is null
            ? null
            : new PostViewResponse
            {
                PostId = id,
                ViewsCount = viewsCount.Value
            };
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var post = await _postRepository.GetByIdAsync(id, cancellationToken);

        if (post is null)
        {
            return false;
        }

        var mediaStorageKeys = post.Media
            .SelectMany(media => new[]
            {
                media.StorageKey,
                media.ThumbnailStorageKey
            })
            .Where(storageKey => !string.IsNullOrWhiteSpace(storageKey))
            .Cast<string>()
            .Distinct()
            .ToArray();

        await _postRepository.DeleteAsync(post, cancellationToken);
        await TryDeleteObjectsAsync(mediaStorageKeys, cancellationToken);

        return true;
    }

    private async Task ApplyMediaAsync(
        Post post,
        Guid authorId,
        IReadOnlyCollection<Guid> mediaIds,
        CancellationToken cancellationToken)
    {
        var distinctMediaIds = mediaIds
            .Distinct()
            .ToArray();

        if (distinctMediaIds.Length != mediaIds.Count)
        {
            throw new InvalidOperationException("Media ids must be unique.");
        }

        IReadOnlyCollection<Media> media = distinctMediaIds.Length == 0
            ? Array.Empty<Media>()
            : await _mediaRepository.GetByIdsAsync(distinctMediaIds, cancellationToken);

        if (media.Count != distinctMediaIds.Length)
        {
            throw new InvalidOperationException("One or more media items were not found.");
        }

        if (media.Any(item => item.UploadedByUserId != authorId))
        {
            throw new InvalidOperationException("One or more media items do not belong to the current user.");
        }

        if (media.Any(item => item.PostId.HasValue && item.PostId != post.Id))
        {
            throw new InvalidOperationException("One or more media items are already attached to another post.");
        }

        foreach (var existingMedia in post.Media.Where(item => !distinctMediaIds.Contains(item.Id)).ToList())
        {
            existingMedia.PostId = null;
            existingMedia.SortOrder = 0;
        }

        post.Media.Clear();

        for (var index = 0; index < distinctMediaIds.Length; index++)
        {
            var currentMedia = media.Single(item => item.Id == distinctMediaIds[index]);
            currentMedia.PostId = post.Id;
            currentMedia.SortOrder = index;
            post.Media.Add(currentMedia);
        }
    }

    private static void ValidatePostCreateRequest(CreatePostRequest request)
    {
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasMedia = request.MediaIds?.Count > 0;
        var hasPoll = request.Poll is not null;
        var hasEmbed = request.Embed is not null;

        if (!hasContent && !hasMedia && !hasPoll && !hasEmbed)
        {
            throw new InvalidOperationException("Post must contain content, media, poll, or embed.");
        }
    }

    private static void ValidatePostState(Post post)
    {
        var hasContent = !string.IsNullOrWhiteSpace(post.Content);
        var hasMedia = post.Media.Count > 0;
        var hasPoll = post.Poll is not null;
        var hasEmbed = !string.IsNullOrWhiteSpace(post.EmbedUrl);

        if (!hasContent && !hasMedia && !hasPoll && !hasEmbed)
        {
            throw new InvalidOperationException("Post must contain content, media, poll, or embed.");
        }
    }

    private static string? NormalizeContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var normalizedContent = content.Trim();

        if (normalizedContent.Length > MaxContentLength)
        {
            throw new InvalidOperationException($"Post content must be {MaxContentLength} characters or less.");
        }

        return normalizedContent;
    }

    private static void ApplyLocation(Post post, PostLocationRequest? location)
    {
        if (location is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(location.Name))
        {
            throw new InvalidOperationException("Location name is required.");
        }

        var normalizedLocationName = location.Name.Trim();

        if (normalizedLocationName.Length > MaxLocationNameLength)
        {
            throw new InvalidOperationException($"Location name must be {MaxLocationNameLength} characters or less.");
        }

        post.LocationName = normalizedLocationName;
        post.LocationPlaceId = NormalizeOptionalValue(location.Id, MaxLocationIdLength, "Location id");
        post.LocationCountry = NormalizeOptionalValue(location.Country, MaxLocationCountryLength, "Location country");
        post.LocationLatitude = location.Latitude;
        post.LocationLongitude = location.Longitude;
    }

    private static void ClearLocation(Post post)
    {
        post.LocationName = null;
        post.LocationPlaceId = null;
        post.LocationCountry = null;
        post.LocationLatitude = null;
        post.LocationLongitude = null;
    }

    private static void ApplyEmbed(Post post, PostEmbedRequest? embed)
    {
        if (embed is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(embed.Url))
        {
            throw new InvalidOperationException("Embed url is required.");
        }

        var normalizedUrl = embed.Url.Trim();

        if (normalizedUrl.Length > MaxEmbedUrlLength)
        {
            throw new InvalidOperationException($"Embed url must be {MaxEmbedUrlLength} characters or less.");
        }

        post.EmbedUrl = normalizedUrl;
        post.EmbedTitle = NormalizeOptionalValue(embed.Title, MaxEmbedTitleLength, "Embed title");
        post.EmbedDescription = NormalizeOptionalValue(embed.Description, MaxEmbedDescriptionLength, "Embed description");
        post.EmbedThumbnailUrl = NormalizeOptionalValue(embed.ThumbnailUrl, MaxEmbedUrlLength, "Embed thumbnail url");
    }

    private static void ClearEmbed(Post post)
    {
        post.EmbedUrl = null;
        post.EmbedTitle = null;
        post.EmbedDescription = null;
        post.EmbedThumbnailUrl = null;
    }

    private static string? NormalizeOptionalValue(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new InvalidOperationException($"{fieldName} must be {maxLength} characters or less.");
        }

        return normalizedValue;
    }

    private static Poll? MapPoll(CreatePostPollRequest? poll)
    {
        if (poll is null)
        {
            return null;
        }

        if (poll.Options is null)
        {
            throw new InvalidOperationException("Poll options are required.");
        }

        if (poll.Options.Count < 2)
        {
            throw new InvalidOperationException("Poll must contain at least 2 options.");
        }

        var normalizedOptions = poll.Options
            .Select(option => option?.Trim())
            .ToList();

        if (normalizedOptions.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("Poll options must not be empty.");
        }

        if (normalizedOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedOptions.Count)
        {
            throw new InvalidOperationException("Poll options must be unique.");
        }

        if (poll.EndsAt.HasValue && poll.EndsAt.Value <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Poll end date must be in the future.");
        }

        return new Poll
        {
            EndsAt = poll.EndsAt.HasValue
                ? new DateTimeOffset(DateTime.SpecifyKind(poll.EndsAt.Value, DateTimeKind.Utc))
                : null,
            Options = normalizedOptions
                .Select((option, index) => new PollOption
                {
                    Text = option!,
                    Position = index
                })
                .ToList()
        };
    }

    private async Task TryDeleteObjectsAsync(
        IEnumerable<string> storageKeys,
        CancellationToken cancellationToken)
    {
        foreach (var storageKey in storageKeys)
        {
            try
            {
                await _objectStorageService.DeleteAsync(storageKey, cancellationToken);
            }
            catch
            {
                // Best-effort cleanup after DB delete already succeeded.
            }
        }
    }

    private PostResponse MapPostResponse(Post post, Guid? currentUserId)
    {
        var response = _mapper.Map<PostResponse>(post);

        return new PostResponse
        {
            Id = response.Id,
            Content = response.Content,
            Author = MapUserShortResponse(post.Author),
            Media = post.Media
                .OrderBy(media => media.SortOrder)
                .Select(media => new PostMediaResponse
                {
                    Id = media.Id,
                    Type = ResolveMediaResponseType(media.ContentType, media.Type),
                    Url = _objectStorageService.GetReadUrl(media.StorageKey),
                    ThumbnailUrl = ResolveMediaThumbnailUrl(media),
                    Width = media.Width,
                    Height = media.Height,
                    Duration = media.DurationSeconds,
                    MimeType = media.ContentType,
                    FileName = media.FileName,
                    SizeInBytes = media.SizeInBytes,
                    SortOrder = media.SortOrder
                })
                .ToList(),
            Poll = MapPollResponse(post.Poll, currentUserId),
            Location = string.IsNullOrWhiteSpace(post.LocationName)
                ? null
                : new PostLocationResponse
                {
                    Id = post.LocationPlaceId,
                    Name = post.LocationName,
                    Country = post.LocationCountry,
                    Latitude = post.LocationLatitude,
                    Longitude = post.LocationLongitude
                },
            Embed = string.IsNullOrWhiteSpace(post.EmbedUrl)
                ? null
                : new PostEmbedResponse
                {
                    Url = post.EmbedUrl,
                    Title = post.EmbedTitle,
                    Description = post.EmbedDescription,
                    ThumbnailUrl = post.EmbedThumbnailUrl
                },
            LikesCount = response.LikesCount,
            CommentsCount = response.CommentsCount,
            RepostsCount = response.RepostsCount,
            ViewsCount = response.ViewsCount,
            BookmarksCount = response.BookmarksCount,
            IsLikedByCurrentUser = currentUserId.HasValue && post.Likes.Any(like => like.UserId == currentUserId.Value),
            IsRepostedByCurrentUser = currentUserId.HasValue && post.Reposts.Any(repost => repost.UserId == currentUserId.Value),
            IsBookmarkedByCurrentUser = currentUserId.HasValue && post.Bookmarks.Any(bookmark => bookmark.UserId == currentUserId.Value),
            CreatedAt = response.CreatedAt,
            UpdatedAt = response.UpdatedAt
        };
    }

    private UserShortResponse MapUserShortResponse(User user)
    {
        var response = _mapper.Map<UserShortResponse>(user);

        return new UserShortResponse
        {
            Id = response.Id,
            Username = response.Username,
            DisplayName = response.DisplayName,
            Location = MapLocation(user),
            AvatarUrl = string.IsNullOrWhiteSpace(user.AvatarObjectKey)
                ? null
                : _objectStorageService.GetReadUrl(user.AvatarObjectKey),
            IsVerified = response.IsVerified
        };
    }

    private static LocationResponse? MapLocation(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Location))
        {
            return null;
        }

        return new LocationResponse
        {
            Id = string.IsNullOrWhiteSpace(user.LocationPlaceId)
                ? user.Location
                : user.LocationPlaceId,
            Name = user.Location,
            Country = user.LocationCountry ?? string.Empty,
            Latitude = user.LocationLatitude ?? 0,
            Longitude = user.LocationLongitude ?? 0
        };
    }

    private static PollResponse? MapPollResponse(Poll? poll, Guid? currentUserId)
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
            EndsAt = poll.EndsAt?.UtcDateTime,
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

    private static string ResolveMediaResponseType(string contentType, Domain.Enums.MediaType mediaType)
    {
        if (contentType.Equals("image/gif", StringComparison.OrdinalIgnoreCase))
        {
            return "gif";
        }

        return mediaType == Domain.Enums.MediaType.Video
            ? "video"
            : "image";
    }

    private string? ResolveMediaThumbnailUrl(Media media)
    {
        if (!string.IsNullOrWhiteSpace(media.ThumbnailStorageKey))
        {
            return _objectStorageService.GetReadUrl(media.ThumbnailStorageKey);
        }

        var responseType = ResolveMediaResponseType(media.ContentType, media.Type);

        return responseType is "image" or "gif"
            ? _objectStorageService.GetReadUrl(media.StorageKey)
            : null;
    }
}
