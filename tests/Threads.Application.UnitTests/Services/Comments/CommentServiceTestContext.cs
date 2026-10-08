using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Users;
using Threads.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Threads.Application.UnitTests.Services.Comments;

internal sealed class CommentServiceTestContext
{
    public CommentServiceTestContext()
    {
        var userResponseFactory = new UserResponseFactory(ObjectStorageService, Mapper);
        ResponseFactory = new CommentResponseFactory(userResponseFactory, ObjectStorageService);
        QueryService = new CommentQueryService(CommentRepository, ResponseFactory);
        ThreadService = new CommentThreadService(CommentRepository, PostService, ResponseFactory);
        MediaManager = new CommentMediaManager(
            MediaRepository,
            ObjectStorageService,
            Substitute.For<ILogger<CommentMediaManager>>());
        Mapper
            .Map<Comment>(Arg.Any<CreateCommentRequest>())
            .Returns(callInfo =>
            {
                var request = callInfo.Arg<CreateCommentRequest>();
                return new Comment
                {
                    Id = Guid.NewGuid(),
                    PostId = request.PostId,
                    Content = request.Content
                };
            });
    }

    public ICommentRepository CommentRepository { get; } = Substitute.For<ICommentRepository>();

    public IPostRepository PostRepository { get; } = Substitute.For<IPostRepository>();

    public IPostService PostService { get; } = Substitute.For<IPostService>();

    public IMediaRepository MediaRepository { get; } = Substitute.For<IMediaRepository>();

    public IObjectStorageService ObjectStorageService { get; } = Substitute.For<IObjectStorageService>();

    public IMapper Mapper { get; } = Substitute.For<IMapper>();

    public CommentResponseFactory ResponseFactory { get; }

    public CommentQueryService QueryService { get; }

    public CommentThreadService ThreadService { get; }

    public CommentMediaManager MediaManager { get; }

    public static CommentSummaryReadModel CreateSummary(
        Guid? id = null,
        Guid? postId = null,
        DateTimeOffset? createdAt = null,
        Guid? parentCommentId = null)
    {
        return new CommentSummaryReadModel
        {
            Id = id ?? Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            PostId = postId ?? Guid.NewGuid(),
            ParentCommentId = parentCommentId,
            Content = "comment",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    public static Comment CreateComment(Guid? authorId = null)
    {
        return new Comment
        {
            Id = Guid.NewGuid(),
            PostId = Guid.NewGuid(),
            AuthorId = authorId ?? Guid.NewGuid(),
            Content = "comment"
        };
    }
}
