using AutoMapper;
using NSubstitute;
using Threads.Application.DTOs.Notifications;
using Threads.Application.DTOs.Pagination;
using Threads.Application.DTOs.Users;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Media;
using Threads.Application.Interfaces.Notifications;
using Threads.Application.Services.Notifications;
using Threads.Application.Services.Users;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Services.Notifications;

public sealed class NotificationServiceTests
{
    private readonly INotificationRepository _repository = Substitute.For<INotificationRepository>();
    private readonly IObjectStorageService _storage = Substitute.For<IObjectStorageService>();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly DateTimeOffset _now = new(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
    private readonly NotificationService _service;

    public NotificationServiceTests()
    {
        var clock = Substitute.For<TimeProvider>();
        clock.GetUtcNow().Returns(_now);
        _service = new NotificationService(_repository,
            new UserResponseFactory(_storage, Substitute.For<IMapper>()), clock);
    }

    [Fact]
    public async Task GetAsync_MapsActorAndPassesExclusiveCursorAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var first = new NotificationReadModel
        {
            Id = Guid.NewGuid(), Type = NotificationType.PostLike, SourceId = Guid.NewGuid(),
            PostId = Guid.NewGuid(), CreatedAt = _now,
            Actor = new UserSummaryReadModel { Id = Guid.NewGuid(), Username = "actor", AvatarObjectKey = "avatar-key" }
        };
        var next = new NotificationReadModel
        {
            Id = Guid.NewGuid(), CreatedAt = _now.AddMinutes(-1),
            Actor = new UserSummaryReadModel { Id = Guid.NewGuid(), Username = "next" }
        };
        _storage.GetReadUrl("avatar-key").Returns("https://example.com/avatar");
        _repository.GetByRecipientIdAsync(_userId, 1, null, cancellation.Token).Returns([first, next]);
        _repository.GetByRecipientIdAsync(_userId, 1, Arg.Is<CursorPosition?>(cursor => cursor != null), cancellation.Token).Returns([next]);

        var page = await _service.GetAsync(_userId, new CursorPageRequest { Limit = 1 }, cancellation.Token);
        var item = Assert.Single(page.Items);
        Assert.Equal(first.Id, item.Id);
        Assert.Equal(first.Type, item.Type);
        Assert.Equal(first.SourceId, item.SourceId);
        Assert.Equal(first.PostId, item.PostId);
        Assert.Equal(first.Actor.Id, item.Actor.Id);
        Assert.Equal("https://example.com/avatar", item.Actor.AvatarUrl);
        Assert.True(page.HasMore);
        Assert.NotNull(page.NextCursor);

        var final = await _service.GetAsync(_userId,
            new CursorPageRequest { Limit = 1, Cursor = page.NextCursor }, cancellation.Token);
        Assert.Equal(next.Id, Assert.Single(final.Items).Id);
        Assert.False(final.HasMore);
        Assert.Null(final.NextCursor);
        await _repository.Received().GetByRecipientIdAsync(_userId, 1,
            Arg.Is<CursorPosition>(cursor => cursor.Id == first.Id && cursor.CreatedAt == first.CreatedAt), cancellation.Token);
    }

    [Theory]
    [InlineData(0, null)]
    [InlineData(51, null)]
    [InlineData(20, "invalid")]
    public async Task GetAsync_InvalidPaginationDoesNotQueryRepository(int limit, string? cursor)
    {
        await Assert.ThrowsAsync<RequestValidationException>(() =>
            _service.GetAsync(_userId, new CursorPageRequest { Limit = limit, Cursor = cursor }));
        await _repository.DidNotReceive().GetByRecipientIdAsync(
            Arg.Any<Guid>(), Arg.Any<int>(), Arg.Any<CursorPosition?>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MarkAsReadAsync_MissingOrForeignNotificationDoesNotWrite(bool exists)
    {
        var notificationId = Guid.NewGuid();
        _repository.GetRecipientIdAsync(notificationId).Returns(exists ? Guid.NewGuid() : (Guid?)null);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.MarkAsReadAsync(_userId, notificationId));
        await _repository.DidNotReceive().MarkAsReadAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<DateTimeOffset>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadOperations_UseAuthenticatedUserServerTimeAndCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var notificationId = Guid.NewGuid();
        _repository.GetRecipientIdAsync(notificationId, cancellation.Token).Returns(_userId);
        _repository.GetUnreadCountAsync(_userId, cancellation.Token).Returns(3);

        Assert.Equal(3, (await _service.GetUnreadCountAsync(_userId, cancellation.Token)).UnreadCount);
        await _service.MarkAsReadAsync(_userId, notificationId, cancellation.Token);
        await _service.MarkAllAsReadAsync(_userId, cancellation.Token);

        await _repository.Received(1).MarkAsReadAsync(notificationId, _userId, _now, cancellation.Token);
        await _repository.Received(1).MarkAllAsReadAsync(_userId, _now, cancellation.Token);
    }
}
