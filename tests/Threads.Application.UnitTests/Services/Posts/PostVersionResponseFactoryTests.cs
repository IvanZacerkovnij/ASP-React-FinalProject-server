using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Media;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Posts;

public sealed class PostVersionResponseFactoryTests
{
    private readonly VersionSnapshotSerializer _serializer = new();

    [Fact]
    public void Create_CombinesHistoricalSnapshotWithCurrentEngagement()
    {
        var firstMedia = CreateMedia(sortOrder: 1);
        var secondMedia = CreateMedia(sortOrder: 0);
        var missingMediaId = Guid.NewGuid();
        var pollId = Guid.NewGuid();
        var firstOptionId = Guid.NewGuid();
        var secondOptionId = Guid.NewGuid();
        var post = new PostSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Content = "current content",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            Poll = new PostPollSummaryReadModel
            {
                Id = pollId,
                TotalVotes = 8,
                SelectedOptionId = secondOptionId,
                Options =
                [
                    new PostPollOptionSummaryReadModel
                    {
                        Id = firstOptionId,
                        Text = "Current first option",
                        Position = 0,
                        VotesCount = 3
                    },
                    new PostPollOptionSummaryReadModel
                    {
                        Id = secondOptionId,
                        Text = "Current second option",
                        Position = 1,
                        VotesCount = 5
                    }
                ]
            },
            LikesCount = 10,
            CommentsCount = 11,
            RepostsCount = 12,
            BookmarksCount = 13,
            ViewsCount = 14,
            IsLikedByCurrentUser = true,
            IsRepostedByCurrentUser = true,
            IsBookmarkedByCurrentUser = true,
            ActionAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var quotedTarget = new object();
        var resolvedQuote = new QuoteResponse
        {
            TargetType = ContentTargetType.Comment,
            TargetId = Guid.NewGuid(),
            TargetVersionId = Guid.NewGuid(),
            HasNewVersion = true,
            ReplyingToUsernames = ["reply-author"],
            Target = quotedTarget
        };
        var snapshot = new PostVersionSnapshot
        {
            Content = "historical content",
            MediaIds = [firstMedia.Id, missingMediaId, secondMedia.Id],
            Poll = new PollVersionSnapshot
            {
                Id = pollId,
                EndsAt = DateTimeOffset.UtcNow.AddDays(1),
                Options =
                [
                    new PollOptionVersionSnapshot
                    {
                        Id = secondOptionId,
                        Text = "Historical second option",
                        Position = 1
                    },
                    new PollOptionVersionSnapshot
                    {
                        Id = firstOptionId,
                        Text = "Historical first option",
                        Position = 0
                    }
                ]
            },
            Location = new LocationVersionSnapshot
            {
                Id = "kyiv",
                Name = "Kyiv",
                Country = "Ukraine"
            },
            LinkPreview = new LinkPreviewVersionSnapshot
            {
                Url = "https://example.com/article",
                Title = "Historical article",
                ThumbnailUrl = "/image.jpg"
            },
            Quote = new QuoteVersionSnapshot
            {
                TargetType = resolvedQuote.TargetType,
                TargetId = resolvedQuote.TargetId,
                TargetVersionId = resolvedQuote.TargetVersionId
            }
        };
        var version = new PostVersion
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            SnapshotJson = _serializer.Serialize(snapshot),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var factory = CreateFactory();

        var result = factory.Create(
            post,
            version,
            new Dictionary<Guid, MediaAttachmentResponse>
            {
                [firstMedia.Id] = firstMedia,
                [secondMedia.Id] = secondMedia
            },
            resolvedQuote);

        Assert.Equal(version.Id, result.VersionId);
        Assert.Equal("historical content", result.Content);
        Assert.Equal([firstMedia.Id, secondMedia.Id], result.Media.Select(media => media.Id));
        Assert.Equal("Historical first option", result.Poll?.Options.First().Text);
        Assert.Equal(8, result.Poll?.TotalVotes);
        Assert.Equal(secondOptionId, result.Poll?.SelectedOptionId);
        Assert.Equal("Kyiv", result.Location?.Name);
        Assert.Equal("https://example.com/image.jpg", result.LinkPreview?.ImageUrl);
        Assert.True(result.Quote?.HasNewVersion);
        Assert.Same(quotedTarget, result.Quote?.Target);
        Assert.Equal(10, result.LikesCount);
        Assert.Equal(14, result.ViewsCount);
        Assert.Null(result.ActionAt);
        Assert.Equal(version.CreatedAt, result.UpdatedAt);
    }

    [Fact]
    public void Create_WhenSchemaIsUnsupported_Throws()
    {
        var post = PostServiceTestContext.CreateSummary();
        var version = new PostVersion
        {
            PostId = post.Id,
            SchemaVersion = 2,
            SnapshotJson = "{}"
        };

        var action = () => CreateFactory().Create(
            post,
            version,
            new Dictionary<Guid, MediaAttachmentResponse>());

        Assert.Throws<InvalidOperationException>(action);
    }

    private PostVersionResponseFactory CreateFactory()
    {
        var userResponseFactory = new UserResponseFactory(
            Substitute.For<IObjectStorageService>(),
            Substitute.For<IMapper>());

        return new PostVersionResponseFactory(_serializer, userResponseFactory);
    }

    private static MediaAttachmentResponse CreateMedia(int sortOrder)
    {
        return new MediaAttachmentResponse
        {
            Id = Guid.NewGuid(),
            Type = "image",
            Url = "https://cdn.example/image.jpg",
            MimeType = "image/jpeg",
            FileName = "image.jpg",
            SizeInBytes = 100,
            SortOrder = sortOrder
        };
    }
}
