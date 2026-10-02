using AutoMapper;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Users;

namespace Threads.Application.UnitTests.Services.Interactions;

internal sealed class InteractionTestContext
{
    public InteractionTestContext()
    {
        var objectStorageService = Substitute.For<IObjectStorageService>();
        var mapper = Substitute.For<IMapper>();
        var userResponseFactory = new UserResponseFactory(objectStorageService, mapper);
        var postResponseFactory = new PostResponseFactory(
            objectStorageService,
            userResponseFactory,
            mapper);
        var commentResponseFactory = new CommentResponseFactory(userResponseFactory);

        PostQueryService = new PostQueryService(
            PostRepository,
            Substitute.For<IUserService>(),
            postResponseFactory,
            Substitute.For<HybridCache>(),
            Substitute.For<ILogger<PostQueryService>>());
        CommentQueryService = new CommentQueryService(
            CommentRepository,
            commentResponseFactory);
    }

    public IPostRepository PostRepository { get; } = Substitute.For<IPostRepository>();

    public ICommentRepository CommentRepository { get; } = Substitute.For<ICommentRepository>();

    public PostQueryService PostQueryService { get; }

    public CommentQueryService CommentQueryService { get; }

    public static PostSummaryReadModel CreatePost(
        DateTimeOffset? actionAt,
        DateTimeOffset? createdAt = null)
    {
        return new PostSummaryReadModel
        {
            Id = Guid.NewGuid(),
            Content = "post",
            Author = CreateUserSummary(),
            ActionAt = actionAt,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public static CommentSummaryReadModel CreateComment(
        DateTimeOffset? actionAt,
        DateTimeOffset? createdAt = null)
    {
        return new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            Content = "comment",
            Author = CreateUserSummary(),
            ActionAt = actionAt,
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    private static UserSummaryReadModel CreateUserSummary()
    {
        return new UserSummaryReadModel
        {
            Id = Guid.NewGuid(),
            Username = $"user-{Guid.NewGuid():N}"
        };
    }
}
