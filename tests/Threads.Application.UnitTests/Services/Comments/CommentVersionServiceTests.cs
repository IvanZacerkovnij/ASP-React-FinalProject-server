using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Users;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentVersionServiceTests
{
    [Fact]
    public async Task GetEditHistoryAsync_ReturnsStoredVersions()
    {
        var repository = Substitute.For<ICommentRepository>();
        var serializer = new VersionSnapshotSerializer();
        var currentComment = new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            Content = "current",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            LikesCount = 7,
            CreatedAt = DateTimeOffset.UtcNow
        };
        var versions = new[]
        {
            CreateVersion(serializer, currentComment.Id, "first"),
            CreateVersion(serializer, currentComment.Id, "second")
        };
        repository.GetSummariesByIdsAsync(
                Arg.Any<IReadOnlyCollection<Guid>>(),
                Arg.Any<Guid?>(),
                Arg.Any<CancellationToken>())
            .Returns([currentComment]);
        repository.GetVersionsAsync(
                currentComment.Id,
                1,
                Arg.Any<CursorPosition?>(),
                Arg.Any<CancellationToken>())
            .Returns(versions);
        var userResponseFactory = new UserResponseFactory(
            Substitute.For<IObjectStorageService>(),
            Substitute.For<IMapper>());
        var service = new CommentVersionService(
            repository,
            new CommentVersionResponseFactory(serializer, userResponseFactory),
            Substitute.For<IMediaRepository>(),
            Substitute.For<IObjectStorageService>());

        var result = await service.GetEditHistoryAsync(
            currentComment.Id,
            new CursorPageRequest { Limit = 1 });

        Assert.NotNull(result);
        Assert.Equal(ContentTargetType.Comment, result.TargetType);
        Assert.Equal(currentComment.Id, result.TargetId);
        Assert.Equal(["first"], result.Versions.Select(version => version.Content));
        Assert.All(result.Versions, version => Assert.Equal(7, version.LikesCount));
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
    }

    private static CommentVersion CreateVersion(
        VersionSnapshotSerializer serializer,
        Guid commentId,
        string content)
    {
        return new CommentVersion
        {
            CommentId = commentId,
            SnapshotJson = serializer.Serialize(new CommentVersionSnapshot
            {
                Content = content
            })
        };
    }
}
