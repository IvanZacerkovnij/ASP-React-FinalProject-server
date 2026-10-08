using Threads.Application.DTOs.Posts.Models;
using Threads.Application.DTOs.Users;
using Threads.Domain.Entities;
using Threads.Domain.Enums;
using MediaEntity = Threads.Domain.Entities.Media;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostResponseFactoryTests
{
    private readonly PostServiceTestContext _context = new();

    [Fact]
    public void Create_FromEntity_MapsEngagementMediaPollAndMetadata()
    {
        var currentUserId = Guid.NewGuid();
        var post = PostServiceTestContext.CreatePost();
        post.LocationName = "Kyiv";
        post.LocationPlaceId = "place-id";
        post.EmbedUrl = "https://example.com";
        post.EmbedTitle = "Example";
        post.PostLikes.Add(new PostLike { UserId = currentUserId, PostId = post.Id });
        post.PostReposts.Add(new PostRepost { UserId = currentUserId, PostId = post.Id });
        post.PostBookmarks.Add(new PostBookmark { UserId = currentUserId, PostId = post.Id });
        post.Media.Add(CreateMedia(sortOrder: 1, MediaType.Video, "video/mp4", "video.mp4", null));
        post.Media.Add(CreateMedia(sortOrder: 0, MediaType.Image, "image/gif", "image.gif", null));
        var option = new PollOption
        {
            Id = Guid.NewGuid(),
            Text = "Option",
            Position = 0
        };
        var vote = new PollVote
        {
            UserId = currentUserId,
            PollOptionId = option.Id
        };
        option.Votes.Add(vote);
        post.Poll = new Poll
        {
            Id = Guid.NewGuid(),
            PostId = post.Id,
            Options = [option],
            Votes = [vote]
        };
        vote.PollId = post.Poll.Id;
        var actionAt = DateTimeOffset.UtcNow;

        var result = _context.ResponseFactory.Create(post, currentUserId, 42, actionAt);

        Assert.Equal(42, result.ViewsCount);
        Assert.True(result.IsLikedByCurrentUser);
        Assert.True(result.IsRepostedByCurrentUser);
        Assert.True(result.IsBookmarkedByCurrentUser);
        Assert.Equal(actionAt, result.ActionAt);
        Assert.Equal([0, 1], result.Media.Select(media => media.SortOrder));
        Assert.Equal("gif", result.Media.First().Type);
        Assert.Equal(result.Media.First().Url, result.Media.First().ThumbnailUrl);
        Assert.Equal("video", result.Media.Last().Type);
        Assert.Null(result.Media.Last().ThumbnailUrl);
        Assert.Equal("Kyiv", result.Location?.Name);
        Assert.Equal("https://example.com/", result.LinkPreview?.Url);
        Assert.Equal("example.com", result.LinkPreview?.Domain);
        Assert.NotNull(result.Poll);
        Assert.True(result.Poll.HasVotedByCurrentUser);
        Assert.Equal(option.Id, result.Poll.SelectedOptionId);
    }

    [Fact]
    public void Create_FromContentAndEngagementReadModels_MergesStableAndDynamicData()
    {
        var optionId = Guid.NewGuid();
        var post = new PostContentReadModel
        {
            Id = Guid.NewGuid(),
            AuthorId = Guid.NewGuid(),
            Content = null,
            Media =
            [
                new PostMediaReadModel
                {
                    Id = Guid.NewGuid(),
                    StorageKey = "videos/video.mp4",
                    ThumbnailStorageKey = "videos/thumbnail.jpg",
                    FileName = "video.mp4",
                    ContentType = "video/mp4",
                    Type = MediaType.Video,
                    SizeInBytes = 100,
                    SortOrder = 0
                }
            ],
            Poll = new PostPollContentReadModel
            {
                Id = Guid.NewGuid(),
                Options =
                [
                    new PostPollOptionContentReadModel
                    {
                        Id = optionId,
                        Text = "Option",
                        Position = 0
                    }
                ]
            },
            CreatedAt = DateTimeOffset.UtcNow
        };
        var engagement = new PostEngagementReadModel
        {
            LikesCount = 3,
            CommentsCount = 4,
            RepostsCount = 5,
            BookmarksCount = 6,
            ViewsCount = 7,
            IsLikedByCurrentUser = true,
            Poll = new PostPollEngagementReadModel
            {
                TotalVotes = 8,
                SelectedOptionId = optionId,
                Options =
                [
                    new PostPollOptionEngagementReadModel
                    {
                        Id = optionId,
                        VotesCount = 8
                    }
                ]
            }
        };
        var author = new UserResponse
        {
            Id = post.AuthorId,
            Username = "author",
            IsVerified = true
        };

        var result = _context.ResponseFactory.Create(post, engagement, author);

        Assert.Equal(string.Empty, result.Content);
        Assert.Equal(3, result.LikesCount);
        Assert.Equal(4, result.CommentsCount);
        Assert.Equal(5, result.RepostsCount);
        Assert.Equal(6, result.BookmarksCount);
        Assert.Equal(7, result.ViewsCount);
        Assert.True(result.IsLikedByCurrentUser);
        Assert.Equal("https://cdn.example/videos/thumbnail.jpg", result.Media.Single().ThumbnailUrl);
        Assert.Equal(8, result.Poll?.TotalVotes);
        Assert.Equal(8, result.Poll?.Options.Single().VotesCount);
    }

    private static MediaEntity CreateMedia(
        int sortOrder,
        MediaType mediaType,
        string contentType,
        string fileName,
        string? thumbnailStorageKey)
    {
        return new MediaEntity
        {
            Id = Guid.NewGuid(),
            StorageKey = $"media/{fileName}",
            ThumbnailStorageKey = thumbnailStorageKey,
            FileName = fileName,
            ContentType = contentType,
            Type = mediaType,
            SizeInBytes = 100,
            SortOrder = sortOrder
        };
    }
}
