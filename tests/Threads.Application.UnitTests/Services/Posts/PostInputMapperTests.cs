using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Services.Posts;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Posts;

public class PostInputMapperTests
{
    [Fact]
    public void Create_WhenRequestHasNoPayload_ThrowsRequestValidationException()
    {
        var exception = Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.Create(Guid.NewGuid(), new CreatePostRequest()));

        Assert.Equal("Post must contain content, media, poll, or link preview.", exception.Message);
    }

    [Fact]
    public void Create_WhenRequestIsValid_NormalizesAndMapsAllFields()
    {
        var authorId = Guid.NewGuid();
        var endsAt = DateTimeOffset.UtcNow.AddDays(1);
        var request = new CreatePostRequest
        {
            Content = "  Hello world  ",
            Location = new PostLocationRequest
            {
                Id = " place-id ",
                Name = " Kyiv ",
                Country = " UA ",
                Latitude = 50.45,
                Longitude = 30.52
            },
            LinkPreview = new LinkPreviewRequest
            {
                Url = " https://example.com ",
                Title = " Title ",
                ImageUrl = " https://example.com/image.jpg "
            },
            Poll = new CreatePostPollRequest
            {
                Options = [" First ", " Second "],
                EndsAt = endsAt
            }
        };

        var result = PostInputMapper.Create(authorId, request);

        Assert.Equal(authorId, result.AuthorId);
        Assert.Equal("Hello world", result.Content);
        Assert.Equal("Kyiv", result.LocationName);
        Assert.Equal("place-id", result.LocationPlaceId);
        Assert.Equal("UA", result.LocationCountry);
        Assert.Equal("https://example.com", result.EmbedUrl);
        Assert.Equal("Title", result.EmbedTitle);
        Assert.Equal("https://example.com/image.jpg", result.EmbedThumbnailUrl);
        Assert.NotNull(result.Poll);
        Assert.Equal(endsAt, result.Poll.EndsAt);
        Assert.Equal(["First", "Second"], result.Poll.Options.Select(option => option.Text));
        Assert.Equal([0, 1], result.Poll.Options.Select(option => option.Position));
    }

    [Fact]
    public void Create_WhenContentExceedsLimit_ThrowsRequestValidationException()
    {
        var request = new CreatePostRequest { Content = new string('x', 301) };

        var exception = Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.Create(Guid.NewGuid(), request));

        Assert.Equal("Post content must be 300 characters or less.", exception.Message);
    }

    [Fact]
    public void Create_WhenPollOptionsAreMissing_ThrowsRequestValidationException()
    {
        var request = new CreatePostRequest
        {
            Poll = new CreatePostPollRequest { Options = null! }
        };

        var exception = Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.Create(Guid.NewGuid(), request));

        Assert.Equal("Poll options are required.", exception.Message);
    }

    [Theory]
    [MemberData(nameof(InvalidPollCases))]
    public void Create_WhenPollIsInvalid_ThrowsExpectedValidationException(
        IReadOnlyCollection<string> options,
        DateTimeOffset? endsAt,
        string expectedMessage)
    {
        var request = new CreatePostRequest
        {
            Poll = new CreatePostPollRequest
            {
                Options = options,
                EndsAt = endsAt
            }
        };

        var exception = Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.Create(Guid.NewGuid(), request));

        Assert.Equal(expectedMessage, exception.Message);
    }

    [Fact]
    public void ApplyMetadataChanges_WhenRemovalFlagsAreProvided_ClearsMetadata()
    {
        var post = new Post
        {
            Content = "content",
            LocationName = "Kyiv",
            LocationPlaceId = "place",
            LocationCountry = "UA",
            LocationLatitude = 50,
            LocationLongitude = 30,
            EmbedUrl = "https://example.com",
            EmbedTitle = "Title",
            EmbedDescription = "Description",
            EmbedThumbnailUrl = "https://example.com/image.jpg"
        };

        PostInputMapper.ApplyMetadataChanges(post, new UpdatePostRequest
        {
            RemovePoll = true,
            RemoveLocation = true,
            RemoveLinkPreview = true
        });

        Assert.Null(post.LocationName);
        Assert.Null(post.LocationPlaceId);
        Assert.Null(post.LocationCountry);
        Assert.Null(post.LocationLatitude);
        Assert.Null(post.LocationLongitude);
        Assert.Null(post.EmbedUrl);
        Assert.Null(post.EmbedTitle);
        Assert.Null(post.EmbedDescription);
        Assert.Null(post.EmbedThumbnailUrl);
    }

    [Fact]
    public void ApplyMetadataChanges_WhenRemovalAndValueAreProvided_ThrowsRequestValidationException()
    {
        var post = new Post { Content = "content" };

        Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.ApplyMetadataChanges(post, new UpdatePostRequest
            {
                RemoveLocation = true,
                Location = new PostLocationRequest { Name = "Kyiv" }
            }));
        Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.ApplyMetadataChanges(post, new UpdatePostRequest
            {
                RemoveLinkPreview = true,
                LinkPreview = new LinkPreviewRequest { Url = "https://example.com" }
            }));
        Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.ApplyMetadataChanges(post, new UpdatePostRequest
            {
                RemovePoll = true,
                Poll = new CreatePostPollRequest { Options = ["One", "Two"] }
            }));
    }

    [Fact]
    public void ApplyMetadataChanges_WhenPostAlreadyHasPoll_ThrowsConflictException()
    {
        var post = new Post { Content = "content", Poll = new Poll() };
        var request = new UpdatePostRequest
        {
            Poll = new CreatePostPollRequest { Options = ["One", "Two"] }
        };

        Assert.Throws<ConflictException>(() => PostInputMapper.ApplyMetadataChanges(post, request));
    }

    [Fact]
    public void ValidateState_WhenPostBecomesEmpty_ThrowsRequestValidationException()
    {
        var exception = Assert.Throws<RequestValidationException>(() =>
            PostInputMapper.ValidateState(new Post()));

        Assert.Equal("Post must contain content, media, poll, or link preview.", exception.Message);
    }

    public static TheoryData<IReadOnlyCollection<string>, DateTimeOffset?, string> InvalidPollCases => new()
    {
        { ["Only one"], null, "Poll must contain at least 2 options." },
        { ["One", " "], null, "Poll options must not be empty." },
        { ["Same", "same"], null, "Poll options must be unique." },
        { ["One", "Two"], DateTimeOffset.UtcNow.AddMinutes(-1), "Poll end date must be in the future." }
    };
}
