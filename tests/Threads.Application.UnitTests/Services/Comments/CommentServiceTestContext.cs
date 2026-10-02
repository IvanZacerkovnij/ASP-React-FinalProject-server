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

namespace Threads.Application.UnitTests.Services.Comments;

internal sealed class CommentServiceTestContext
{
    public CommentServiceTestContext()
    {
        var objectStorageService = Substitute.For<IObjectStorageService>();
        var userResponseFactory = new UserResponseFactory(objectStorageService, Mapper);
        ResponseFactory = new CommentResponseFactory(userResponseFactory);
        QueryService = new CommentQueryService(CommentRepository, ResponseFactory);
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

    public IMapper Mapper { get; } = Substitute.For<IMapper>();

    public CommentResponseFactory ResponseFactory { get; }

    public CommentQueryService QueryService { get; }

    public static CommentSummaryReadModel CreateSummary(
        Guid? id = null,
        Guid? postId = null,
        DateTimeOffset? createdAt = null)
    {
        return new CommentSummaryReadModel
        {
            Id = id ?? Guid.NewGuid(),
            PostId = postId ?? Guid.NewGuid(),
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
