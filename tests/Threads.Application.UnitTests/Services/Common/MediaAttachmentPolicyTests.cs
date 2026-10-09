using Microsoft.Extensions.Logging;
using NSubstitute;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Posts;
using Threads.Application.Services.ScheduledPosts;
using Threads.Domain.Entities;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Common;

public sealed class MediaAttachmentPolicyTests
{
    [Theory]
    [InlineData("post", 4)]
    [InlineData("post", 5)]
    [InlineData("comment", 4)]
    [InlineData("comment", 5)]
    [InlineData("scheduled", 4)]
    [InlineData("scheduled", 5)]
    public async Task Managers_EnforceLimitBeforeQueryingOrChangingAttachments(string kind, int count)
    {
        var authorId = Guid.NewGuid();
        var repository = Substitute.For<IMediaRepository>();
        var storage = Substitute.For<IObjectStorageService>();
        var post = new Post { AuthorId = authorId };
        var comment = new Comment { AuthorId = authorId };
        var scheduled = new ScheduledPost { AuthorId = authorId };
        var existing = new MediaEntity { Id = Guid.NewGuid(), UploadedByUserId = authorId };
        ICollection<MediaEntity> attachments = kind switch
        {
            "post" => post.Media,
            "comment" => comment.Media,
            _ => scheduled.Media
        };
        attachments.Add(existing);
        var media = Enumerable.Range(0, count).Select(_ => new MediaEntity
        {
            Id = Guid.NewGuid(), UploadedByUserId = authorId
        }).ToArray();
        var ids = media.Select(item => item.Id).ToArray();
        using var cancellation = new CancellationTokenSource();
        repository.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), cancellation.Token).Returns(media);

        Func<Task> apply = kind switch
        {
            "post" => () => new PostMediaManager(repository, storage, Substitute.For<ILogger<PostMediaManager>>())
                .ApplyAsync(post, authorId, ids, cancellation.Token),
            "comment" => () => new CommentMediaManager(repository, storage, Substitute.For<ILogger<CommentMediaManager>>())
                .ApplyAsync(comment, authorId, ids, cancellation.Token),
            _ => () => new ScheduledPostMediaManager(repository).ApplyAsync(scheduled, authorId, ids, cancellation.Token)
        };

        if (count > 4)
        {
            await Assert.ThrowsAsync<RequestValidationException>(apply);
            Assert.Same(existing, Assert.Single(attachments));
            await repository.DidNotReceive().GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>());
        }
        else
        {
            await apply();
            Assert.Equal(ids, attachments.Select(item => item.Id));
            Assert.Equal(new[] { 0, 1, 2, 3 }, attachments.Select(item => item.SortOrder));
            await repository.Received(1).GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), cancellation.Token);
        }
    }
}
