using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.ScheduledPosts;
using Threads.Application.Services.Posts;

namespace Threads.Application.UnitTests.DTOs.Validation;

public sealed class PostContentLimitTests
{
    [Theory]
    [InlineData(300, true)]
    [InlineData(301, false)]
    public void NormalAndScheduledPostRequests_EnforceSameTextLimit(int length, bool expectedValid)
    {
        var content = new string('x', length);
        object[] requests =
        [
            new CreatePostRequest { Content = content },
            new UpdatePostRequest { Content = content },
            new CreateScheduledPostRequest { Content = content, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1) },
            new UpdateScheduledPostRequest { Content = content }
        ];

        Assert.All(requests, request => Assert.Equal(expectedValid,
            Validator.TryValidateObject(request, new ValidationContext(request), [], validateAllProperties: true)));
    }

    [Fact]
    public void PostBusinessLogic_AcceptsExactlyThreeHundredCharacters()
    {
        var content = new string('x', 300);

        var post = PostInputMapper.Create(Guid.NewGuid(), new CreatePostRequest { Content = content });

        Assert.Equal(content, post.Content);
    }
}
