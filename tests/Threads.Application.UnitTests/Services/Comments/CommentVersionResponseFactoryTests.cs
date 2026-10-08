using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Users;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentVersionResponseFactoryTests
{
    private readonly VersionSnapshotSerializer _serializer = new();

    [Fact]
    public void Create_CombinesHistoricalSnapshotWithCurrentEngagement()
    {
        var comment = new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            ParentCommentId = Guid.NewGuid(),
            Content = "current content",
            LinkPreviewUrl = "https://current.example",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            LikesCount = 3,
            IsLikedByCurrentUser = true,
            RepliesCount = 4,
            IsBookmarkedByCurrentUser = true,
            RepostsCount = 5,
            IsRepostedByCurrentUser = true,
            ViewsCount = 6,
            ActionAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-2)
        };
        var snapshot = new CommentVersionSnapshot
        {
            Content = "historical content",
            LinkPreview = new LinkPreviewVersionSnapshot
            {
                Url = "https://example.com/article",
                Title = "Historical article",
                ThumbnailUrl = "/image.jpg"
            }
        };
        var version = new CommentVersion
        {
            Id = Guid.NewGuid(),
            CommentId = comment.Id,
            SnapshotJson = _serializer.Serialize(snapshot),
            CreatedAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var userResponseFactory = new UserResponseFactory(
            Substitute.For<IObjectStorageService>(),
            Substitute.For<IMapper>());
        var factory = new CommentVersionResponseFactory(_serializer, userResponseFactory);

        var result = factory.Create(comment, version);

        Assert.Equal(version.Id, result.VersionId);
        Assert.Equal("historical content", result.Content);
        Assert.Equal("Historical article", result.LinkPreview?.Title);
        Assert.Equal("https://example.com/image.jpg", result.LinkPreview?.ImageUrl);
        Assert.Equal(3, result.LikesCount);
        Assert.Equal(4, result.RepliesCount);
        Assert.Equal(5, result.RepostsCount);
        Assert.Equal(6, result.ViewsCount);
        Assert.Null(result.ActionAt);
        Assert.Equal(version.CreatedAt, result.UpdatedAt);
    }

    [Fact]
    public void Create_WhenVersionBelongsToAnotherComment_Throws()
    {
        var comment = new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            Content = "content",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            CreatedAt = DateTimeOffset.UtcNow
        };
        var version = new CommentVersion
        {
            CommentId = Guid.NewGuid(),
            SnapshotJson = _serializer.Serialize(new CommentVersionSnapshot
            {
                Content = "historical content"
            })
        };
        var userResponseFactory = new UserResponseFactory(
            Substitute.For<IObjectStorageService>(),
            Substitute.For<IMapper>());
        var factory = new CommentVersionResponseFactory(_serializer, userResponseFactory);

        var action = () => factory.Create(comment, version);

        Assert.Throws<ArgumentException>(action);
    }
}
