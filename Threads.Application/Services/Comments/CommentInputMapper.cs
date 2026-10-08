using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Services.Common;
using Threads.Domain.Entities;

namespace Threads.Application.Services.Comments;

internal static class CommentInputMapper
{
    private const int MaxLocationNameLength = 255;
    private const int MaxLocationCountryLength = 255;
    private const int MaxLocationIdLength = 1024;
    private const int MaxLinkPreviewUrlLength = 2048;
    private const int MaxLinkPreviewTitleLength = 255;

    public static void ApplyCreateMetadata(Comment comment, CreateCommentRequest request)
    {
        ApplyLocation(comment, request.Location);
        ApplyLinkPreview(comment, request.LinkPreview);
        comment.Poll = MapPoll(request.Poll);
    }

    public static Poll? ApplyMetadataChanges(Comment comment, UpdateCommentRequest request)
    {
        ValidateMetadataChanges(request);

        Poll? removedPoll = null;

        if (request.RemoveLocation)
        {
            ApplyLocation(comment, null);
        }
        else if (request.Location is not null)
        {
            ApplyLocation(comment, request.Location);
        }

        if (request.RemoveLinkPreview)
        {
            ApplyLinkPreview(comment, null);
        }
        else if (request.LinkPreview is not null)
        {
            ApplyLinkPreview(comment, request.LinkPreview);
        }

        if (request.RemovePoll)
        {
            removedPoll = comment.Poll;
            comment.Poll = null;
        }
        else if (request.Poll is not null)
        {
            if (comment.Poll is not null)
            {
                throw new ConflictException("Updating an existing poll is not supported.");
            }

            comment.Poll = MapPoll(request.Poll);
        }

        return removedPoll;
    }

    private static void ValidateMetadataChanges(UpdateCommentRequest request)
    {
        if (request.RemovePoll && request.Poll is not null)
        {
            throw new RequestValidationException(
                "Poll cannot be provided when poll removal is requested.");
        }

        if (request.RemoveLocation && request.Location is not null)
        {
            throw new RequestValidationException(
                "Location cannot be provided when location removal is requested.");
        }

        if (request.RemoveLinkPreview && request.LinkPreview is not null)
        {
            throw new RequestValidationException(
                "Link preview cannot be provided when link preview removal is requested.");
        }
    }

    private static void ApplyLocation(Comment comment, PostLocationRequest? location)
    {
        if (location is null)
        {
            comment.LocationName = null;
            comment.LocationPlaceId = null;
            comment.LocationCountry = null;
            comment.LocationLatitude = null;
            comment.LocationLongitude = null;
            return;
        }

        comment.LocationName = InputNormalizer.NormalizeRequired(
            location.Name,
            MaxLocationNameLength,
            "Location name");
        comment.LocationPlaceId = InputNormalizer.NormalizeOptional(
            location.Id,
            MaxLocationIdLength,
            "Location id");
        comment.LocationCountry = InputNormalizer.NormalizeOptional(
            location.Country,
            MaxLocationCountryLength,
            "Location country");
        comment.LocationLatitude = location.Latitude;
        comment.LocationLongitude = location.Longitude;
    }

    private static void ApplyLinkPreview(Comment comment, LinkPreviewRequest? linkPreview)
    {
        if (linkPreview is null)
        {
            comment.LinkPreviewUrl = null;
            comment.LinkPreviewTitle = null;
            comment.LinkPreviewImageUrl = null;
            return;
        }

        comment.LinkPreviewUrl = InputNormalizer.NormalizeRequired(
            linkPreview.Url,
            MaxLinkPreviewUrlLength,
            "Link preview url");
        comment.LinkPreviewTitle = InputNormalizer.NormalizeOptional(
            linkPreview.Title,
            MaxLinkPreviewTitleLength,
            "Link preview title");
        comment.LinkPreviewImageUrl = InputNormalizer.NormalizeOptional(
            linkPreview.ImageUrl,
            MaxLinkPreviewUrlLength,
            "Link preview image url");
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
