using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.Exceptions;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Posts;

internal static class PostInputMapper
{
    private const int MaxContentLength = 2000;
    private const int MaxLocationNameLength = 255;
    private const int MaxLocationCountryLength = 255;
    private const int MaxLocationIdLength = 1024;
    private const int MaxEmbedUrlLength = 2048;
    private const int MaxEmbedTitleLength = 255;

    public static Post Create(Guid authorId, CreatePostRequest request)
    {
        ValidateCreateRequest(request);

        var post = new Post
        {
            AuthorId = authorId,
            Content = NormalizeContent(request.Content),
            Poll = MapPoll(request.Poll)
        };

        ApplyLocation(post, request.Location);
        ApplyLinkPreview(post, request.LinkPreview);

        return post;
    }

    public static void ApplyContent(Post post, string? content)
    {
        if (content is not null)
        {
            post.Content = NormalizeContent(content);
        }
    }

    public static void ApplyMetadataChanges(Post post, UpdatePostRequest request)
    {
        if (request.RemoveLocation)
        {
            ClearLocation(post);
        }
        else if (request.Location is not null)
        {
            ApplyLocation(post, request.Location);
        }

        if (request.HasLinkPreviewValue)
        {
            if (request.LinkPreview is null)
            {
                ClearLinkPreview(post);
            }
            else
            {
                ApplyLinkPreview(post, request.LinkPreview);
            }
        }

        if (request.Poll is not null)
        {
            if (post.Poll is not null)
            {
                throw new ConflictException("Updating an existing poll is not supported.");
            }

            post.Poll = MapPoll(request.Poll);
        }
    }

    public static void ValidateState(Post post)
    {
        var hasContent = !string.IsNullOrWhiteSpace(post.Content);
        var hasMedia = post.Media.Count > 0;
        var hasPoll = post.Poll is not null;
        var hasLinkPreview = !string.IsNullOrWhiteSpace(post.EmbedUrl);

        if (!hasContent && !hasMedia && !hasPoll && !hasLinkPreview)
        {
            throw new RequestValidationException("Post must contain content, media, poll, or link preview.");
        }
    }

    private static void ValidateCreateRequest(CreatePostRequest request)
    {
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasMedia = request.MediaIds?.Count > 0;
        var hasPoll = request.Poll is not null;
        var hasLinkPreview = request.LinkPreview is not null;

        if (!hasContent && !hasMedia && !hasPoll && !hasLinkPreview)
        {
            throw new RequestValidationException("Post must contain content, media, poll, or link preview.");
        }
    }

    private static string? NormalizeContent(string? content)
    {
        return InputNormalizer.NormalizeOptional(
            content,
            MaxContentLength,
            "Post content");
    }

    private static void ApplyLocation(Post post, PostLocationRequest? location)
    {
        if (location is null)
        {
            return;
        }

        post.LocationName = InputNormalizer.NormalizeRequired(
            location.Name,
            MaxLocationNameLength,
            "Location name");
        post.LocationPlaceId = InputNormalizer.NormalizeOptional(
            location.Id,
            MaxLocationIdLength,
            "Location id");
        post.LocationCountry = InputNormalizer.NormalizeOptional(
            location.Country,
            MaxLocationCountryLength,
            "Location country");
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

    private static void ApplyLinkPreview(Post post, LinkPreviewRequest? linkPreview)
    {
        if (linkPreview is null)
        {
            return;
        }

        post.EmbedUrl = InputNormalizer.NormalizeRequired(
            linkPreview.Url,
            MaxEmbedUrlLength,
            "Link preview url");
        post.EmbedTitle = InputNormalizer.NormalizeOptional(
            linkPreview.Title,
            MaxEmbedTitleLength,
            "Link preview title");
        post.EmbedDescription = null;
        post.EmbedThumbnailUrl = InputNormalizer.NormalizeOptional(
            linkPreview.ImageUrl,
            MaxEmbedUrlLength,
            "Link preview image url");
    }

    private static void ClearLinkPreview(Post post)
    {
        post.EmbedUrl = null;
        post.EmbedTitle = null;
        post.EmbedDescription = null;
        post.EmbedThumbnailUrl = null;
    }

    private static Poll? MapPoll(CreatePostPollRequest? poll)
    {
        if (poll is null)
        {
            return null;
        }

        if (poll.Options is null)
        {
            throw new RequestValidationException("Poll options are required.");
        }

        if (poll.Options.Count < 2)
        {
            throw new RequestValidationException("Poll must contain at least 2 options.");
        }

        var normalizedOptions = poll.Options
            .Select(option => option?.Trim())
            .ToList();

        if (normalizedOptions.Any(string.IsNullOrWhiteSpace))
        {
            throw new RequestValidationException("Poll options must not be empty.");
        }

        if (normalizedOptions.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalizedOptions.Count)
        {
            throw new RequestValidationException("Poll options must be unique.");
        }

        if (poll.EndsAt.HasValue && poll.EndsAt.Value <= DateTimeOffset.UtcNow)
        {
            throw new RequestValidationException("Poll end date must be in the future.");
        }

        return new Poll
        {
            EndsAt = poll.EndsAt,
            Options = normalizedOptions
                .Select((option, index) => new PollOption
                {
                    Text = option!,
                    Position = index
                })
                .ToList()
        };
    }
}
