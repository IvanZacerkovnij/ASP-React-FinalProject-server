using System.Text.Json;
using Microsoft.Extensions.Options;
using NSubstitute;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Quotes;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Recommendations;
using Threads.Application.Recommendations;
using Threads.Application.Services.Comments;
using Threads.Application.Services.Recommendations;
using Threads.Application.Services.Users;
using Threads.Application.UnitTests.Services.Posts;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Recommendations;

public sealed class RecommendationServiceTests
{
    private readonly Guid _viewerId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);
    private readonly IRecommendationRepository _repository = Substitute.For<IRecommendationRepository>();
    private readonly IRecommendationCursorProtector _protector = Substitute.For<IRecommendationCursorProtector>();
    private readonly PostServiceTestContext _posts = new();
    private readonly RecommendationService _service;

    public RecommendationServiceTests()
    {
        var time = Substitute.For<TimeProvider>();
        time.GetUtcNow().Returns(_now);
        _protector.Protect(Arg.Any<string>()).Returns(call => call.Arg<string>());
        _protector.Unprotect(Arg.Any<string>()).Returns(call => call.Arg<string>());
        _repository.GetAvailableIdsAsync(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyCollection<Guid>>());
        _repository.GetUsersAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IReadOnlyCollection<Guid>>().Reverse()
                .Select(id => new UserSummaryReadModel { Id = id, Username = id.ToString() }).ToArray());
        var userFactory = new UserResponseFactory(_posts.ObjectStorageService, _posts.Mapper);
        _service = new RecommendationService(_repository, _protector, _posts.PostRepository,
            _posts.CommentRepository, _posts.ResponseFactory,
            new CommentResponseFactory(userFactory, _posts.ObjectStorageService), userFactory,
            Options.Create(new RecommendationOptions()), time);
    }

    [Fact]
    public async Task Users_CursorRetainsRankingAndEliminatesDuplicatesWithoutReranking()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var thirdId = Guid.NewGuid();
        _repository.RankUsersAsync(_viewerId, _now, Arg.Any<CancellationToken>())
            .Returns([firstId, firstId, secondId, thirdId]);

        var first = await _service.GetUsersAsync(_viewerId, new CursorPageRequest { Limit = 1 });
        _repository.RankUsersAsync(_viewerId, _now, Arg.Any<CancellationToken>()).Returns([thirdId, firstId]);
        var second = await _service.GetUsersAsync(_viewerId, new CursorPageRequest { Limit = 2, Cursor = first.NextCursor });

        Assert.Equal(firstId, Assert.Single(first.Items).Id);
        Assert.True(first.HasMore);
        Assert.Equal(new[] { secondId, thirdId }, second.Items.Select(user => user.Id));
        Assert.False(second.HasMore);
        Assert.Null(second.NextCursor);
        await _repository.Received(1).RankUsersAsync(_viewerId, _now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Users_NextPageRechecksAvailabilityAndSkipsNewlyInaccessibleAccounts()
    {
        var firstId = Guid.NewGuid();
        var removedId = Guid.NewGuid();
        var lastId = Guid.NewGuid();
        _repository.RankUsersAsync(_viewerId, _now, Arg.Any<CancellationToken>()).Returns([firstId, removedId, lastId]);
        var first = await _service.GetUsersAsync(_viewerId, new CursorPageRequest { Limit = 1 });
        _repository.GetAvailableIdsAsync(_viewerId, "users", Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns([lastId]);

        var second = await _service.GetUsersAsync(_viewerId, new CursorPageRequest { Limit = 1, Cursor = first.NextCursor });

        Assert.Equal(lastId, Assert.Single(second.Items).Id);
        Assert.False(second.HasMore);
    }

    [Theory]
    [InlineData("viewer")]
    [InlineData("kind")]
    [InlineData("expired")]
    [InlineData("oversized")]
    public async Task Users_RejectsCursorWithWrongScopeOrExpiredSnapshot(string invalidPart)
    {
        var snapshot = new RecommendationSnapshot(
            invalidPart == "viewer" ? Guid.NewGuid() : _viewerId,
            invalidPart == "kind" ? "posts" : "users",
            invalidPart == "expired" ? _now : _now.AddMinutes(10),
            invalidPart == "oversized" ? new Guid[501] : [Guid.NewGuid()]);

        await Assert.ThrowsAsync<RequestValidationException>(() => _service.GetUsersAsync(_viewerId,
            new CursorPageRequest { Cursor = JsonSerializer.Serialize(snapshot) }));
        await _repository.DidNotReceive().RankUsersAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("null")]
    public async Task Users_RejectsMalformedCursor(string cursor)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.GetUsersAsync(_viewerId,
            new CursorPageRequest { Cursor = cursor }));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Users_RejectsInvalidLimit(int limit)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.GetUsersAsync(_viewerId,
            new CursorPageRequest { Limit = limit }));
    }

    [Fact]
    public async Task Users_EmptyGraphReturnsEmptyEnvelope()
    {
        _repository.RankUsersAsync(_viewerId, _now, Arg.Any<CancellationToken>()).Returns([]);

        var page = await _service.GetUsersAsync(_viewerId, new CursorPageRequest());

        Assert.Empty(page.Items);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public async Task Posts_HydratesRepeatedQuoteTargetsInOneBatchAndPassesCancellation()
    {
        var target = new CommentSummaryReadModel
        {
            Id = Guid.NewGuid(), VersionId = Guid.NewGuid(), PostId = Guid.NewGuid(), Content = "quoted comment",
            Author = new UserSummaryReadModel { Id = Guid.NewGuid(), Username = "quoted-author" }
        };
        var quote = new QuoteReadModel
        {
            TargetType = ContentTargetType.Comment, TargetId = target.Id, TargetVersionId = Guid.NewGuid()
        };
        var first = new PostSummaryReadModel
        {
            Id = Guid.NewGuid(), Author = target.Author, Quote = quote
        };
        var second = new PostSummaryReadModel
        {
            Id = Guid.NewGuid(), Author = target.Author, Quote = quote
        };
        using var cancellation = new CancellationTokenSource();
        _repository.RankPostsAsync(_viewerId, _now, cancellation.Token).Returns([first.Id, second.Id]);
        _posts.PostRepository.GetSummariesByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), _viewerId, cancellation.Token)
            .Returns([second, first]);
        _posts.CommentRepository.GetSummariesByIdsAsync(
            Arg.Any<IReadOnlyCollection<Guid>>(), _viewerId, cancellation.Token).Returns([target]);

        var page = await _service.GetPostsAsync(_viewerId, new CursorPageRequest(), cancellation.Token);

        Assert.Equal(new[] { first.Id, second.Id }, page.Items.Select(post => post.Id));
        Assert.All(page.Items, post =>
        {
            Assert.True(post.Quote!.HasNewVersion);
            Assert.IsType<CommentResponse>(post.Quote.Target);
        });
        await _posts.CommentRepository.Received(1).GetSummariesByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(target.Id)), _viewerId, cancellation.Token);
    }

    [Fact]
    public void Options_RejectsStrongViewsAndInconsistentCandidateLimits()
    {
        Assert.True(new RecommendationOptions().HasValidWeights());
        Assert.False(new RecommendationOptions { ViewWeight = 3 }.HasValidWeights());
        Assert.False(new RecommendationOptions { CandidateLimit = 50, ResultLimit = 200 }.HasValidWeights());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Feed_UsesDatabaseSelectionAndPreservesOrderWithoutDuplicates(bool authenticated)
    {
        var viewer = authenticated ? _viewerId : (Guid?)null;
        var first = PostServiceTestContext.CreateSummary();
        var second = PostServiceTestContext.CreateSummary();
        using var cancellation = new CancellationTokenSource();
        _repository.GetFeedIdsAsync(viewer, 10, _now, cancellation.Token)
            .Returns([first.Id, first.Id, second.Id]);
        _posts.PostRepository.GetSummariesByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2), viewer, cancellation.Token)
            .Returns([second, first]);

        var feed = await _service.GetFeedAsync(viewer, 10, cancellation.Token);

        Assert.Equal(new[] { first.Id, second.Id }, feed.Select(post => post.Id));
        await _repository.Received(1).GetFeedIdsAsync(viewer, 10, _now, cancellation.Token);
        await _repository.DidNotReceive().RankPostsAsync(Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task Feed_RejectsInvalidCount(int count)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() => _service.GetFeedAsync(null, count));
    }
}
