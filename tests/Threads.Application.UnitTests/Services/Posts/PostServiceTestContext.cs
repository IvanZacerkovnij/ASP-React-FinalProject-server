using AutoMapper;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Posts.Responses;
using Threads.Application.DTOs.Users;
using Threads.Application.Interfaces.Comments;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Posts;
using Threads.Application.Interfaces.Users;
using Threads.Application.Services.Posts;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Users;
using Threads.Application.UnitTests.TestSupport;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Posts;

internal sealed class PostServiceTestContext
{
    public PostServiceTestContext()
    {
        ObjectStorageService
            .GetReadUrl(Arg.Any<string>())
            .Returns(callInfo => $"https://cdn.example/{callInfo.Arg<string>()}");
        Mapper
            .Map<UserShortResponse>(Arg.Any<User>())
            .Returns(callInfo =>
            {
                var user = callInfo.Arg<User>();
                return new UserShortResponse
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    IsVerified = user.IsVerified
                };
            });
        Mapper
            .Map<PostResponse>(Arg.Any<Post>())
            .Returns(callInfo => MapPost(callInfo.Arg<Post>()));

        var userResponseFactory = new UserResponseFactory(ObjectStorageService, Mapper);
        ResponseFactory = new PostResponseFactory(
            ObjectStorageService,
            userResponseFactory,
            Mapper);
        var commentQueryService = new CommentQueryService(
            CommentRepository,
            new CommentResponseFactory(userResponseFactory, ObjectStorageService));
        QueryService = new PostQueryService(
            PostRepository,
            UserService,
            ResponseFactory,
            commentQueryService,
            Cache,
            Substitute.For<ILogger<PostQueryService>>());
    }

    public IPostRepository PostRepository { get; } = Substitute.For<IPostRepository>();

    public ICommentRepository CommentRepository { get; } = Substitute.For<ICommentRepository>();

    public IObjectStorageService ObjectStorageService { get; } = Substitute.For<IObjectStorageService>();

    public IUserService UserService { get; } = Substitute.For<IUserService>();

    public IMapper Mapper { get; } = Substitute.For<IMapper>();

    public TestHybridCache Cache { get; } = new();

    public PostResponseFactory ResponseFactory { get; }

    public PostQueryService QueryService { get; }

    public static User CreateUser()
    {
        return new User
        {
            Id = Guid.NewGuid(),
            Email = "author@example.com",
            Username = "author",
            PasswordHash = "password-hash",
            IsVerified = true
        };
    }

    public static Post CreatePost(Guid? authorId = null)
    {
        var author = CreateUser();
        if (authorId.HasValue)
        {
            author.Id = authorId.Value;
        }

        return new Post
        {
            Id = Guid.NewGuid(),
            AuthorId = author.Id,
            Author = author,
            Content = "post content"
        };
    }

    public static PostSummaryReadModel CreateSummary(DateTimeOffset? createdAt = null)
    {
        return new PostSummaryReadModel
        {
            Id = Guid.NewGuid(),
            VersionId = Guid.NewGuid(),
            Content = "post content",
            Author = new UserSummaryReadModel
            {
                Id = Guid.NewGuid(),
                Username = "author"
            },
            CreatedAt = createdAt ?? DateTimeOffset.UtcNow
        };
    }

    private static PostResponse MapPost(Post post)
    {
        return new PostResponse
        {
            Id = post.Id,
            VersionId = post.CurrentVersionId,
            Content = post.Content ?? string.Empty,
            Author = new UserShortResponse
            {
                Id = post.AuthorId,
                Username = post.Author?.Username ?? "author"
            },
            LikesCount = post.PostLikes.Count,
            CommentsCount = post.Comments.Count,
            RepostsCount = post.PostReposts.Count,
            BookmarksCount = post.PostBookmarks.Count,
            CreatedAt = post.CreatedAt,
            UpdatedAt = post.UpdatedAt
        };
    }
}
