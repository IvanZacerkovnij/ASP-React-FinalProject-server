using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
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
    private const int MaxEmbedDescriptionLength = 1000;

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
        ApplyEmbed(post, request.Embed);

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
        var hasEmbed = !string.IsNullOrWhiteSpace(post.EmbedUrl);

        if (!hasContent && !hasMedia && !hasPoll && !hasEmbed)
        {
            throw new RequestValidationException("Post must contain content, media, poll, or embed.");
        }
    }

    private static void ValidateCreateRequest(CreatePostRequest request)
    {
        var hasContent = !string.IsNullOrWhiteSpace(request.Content);
        var hasMedia = request.MediaIds?.Count > 0;
        var hasPoll = request.Poll is not null;
        var hasEmbed = request.Embed is not null;

        if (!hasContent && !hasMedia && !hasPoll && !hasEmbed)
        {
            throw new RequestValidationException("Post must contain content, media, poll, or embed.");
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
            throw new RequestValidationException(
                $"Post content must be {MaxContentLength} characters or less.");
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
            throw new RequestValidationException("Location name is required.");
        }

        var normalizedLocationName = location.Name.Trim();

        if (normalizedLocationName.Length > MaxLocationNameLength)
        {
            throw new RequestValidationException(
                $"Location name must be {MaxLocationNameLength} characters or less.");
        }

        post.LocationName = normalizedLocationName;
        post.LocationPlaceId = NormalizeOptionalValue(location.Id, MaxLocationIdLength, "Location id");
        post.LocationCountry = NormalizeOptionalValue(
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

    private static void ApplyEmbed(Post post, PostEmbedRequest? embed)
    {
        if (embed is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(embed.Url))
        {
            throw new RequestValidationException("Embed url is required.");
        }

        var normalizedUrl = embed.Url.Trim();

        if (normalizedUrl.Length > MaxEmbedUrlLength)
        {
            throw new RequestValidationException(
                $"Embed url must be {MaxEmbedUrlLength} characters or less.");
        }

        post.EmbedUrl = normalizedUrl;
        post.EmbedTitle = NormalizeOptionalValue(embed.Title, MaxEmbedTitleLength, "Embed title");
        post.EmbedDescription = NormalizeOptionalValue(
            embed.Description,
            MaxEmbedDescriptionLength,
            "Embed description");
        post.EmbedThumbnailUrl = NormalizeOptionalValue(
            embed.ThumbnailUrl,
            MaxEmbedUrlLength,
            "Embed thumbnail url");
    }

    private static void ClearEmbed(Post post)
    {
        post.EmbedUrl = null;
        post.EmbedTitle = null;
        post.EmbedDescription = null;
        post.EmbedThumbnailUrl = null;
    }

    private static string? NormalizeOptionalValue(
        string? value,
        int maxLength,
        string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalizedValue = value.Trim();

        if (normalizedValue.Length > maxLength)
        {
            throw new RequestValidationException(
                $"{fieldName} must be {maxLength} characters or less.");
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

        if (poll.EndsAt.HasValue && poll.EndsAt.Value <= DateTime.UtcNow)
        {
            throw new RequestValidationException("Poll end date must be in the future.");
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
}
