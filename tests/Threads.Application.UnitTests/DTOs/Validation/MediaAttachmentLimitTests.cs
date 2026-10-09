using System.ComponentModel.DataAnnotations;
using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.DTOs.ScheduledPosts;

namespace Threads.Application.UnitTests.DTOs.Validation;

public sealed class MediaAttachmentLimitTests
{
    public static IEnumerable<object[]> Requests()
    {
        foreach (var count in new[] { 0, 4, 5 })
        {
            var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).ToArray();
            yield return [new CreatePostRequest { Content = "post", MediaIds = ids }, count <= 4];
            yield return [new UpdatePostRequest { Content = "post", MediaIds = ids }, count <= 4];
            yield return [new CreateCommentRequest { PostId = Guid.NewGuid(), Content = "comment", MediaIds = ids }, count <= 4];
            yield return [new UpdateCommentRequest { Content = "comment", MediaIds = ids }, count <= 4];
            yield return [new CreateScheduledPostRequest { Content = "post", MediaIds = ids, ScheduledAt = DateTimeOffset.UtcNow.AddDays(1) }, count <= 4];
            yield return [new UpdateScheduledPostRequest { MediaIds = ids }, count <= 4];
        }
    }

    [Theory]
    [MemberData(nameof(Requests))]
    public void Requests_AllowUpToFourMediaAndRejectFive(object request, bool expectedValid)
    {
        var errors = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(request, new ValidationContext(request), errors, validateAllProperties: true);

        Assert.Equal(expectedValid, valid);
        if (!expectedValid)
        {
            Assert.Contains(errors, error => error.MemberNames.Contains("MediaIds"));
        }
    }

    [Fact]
    public void Updates_OmittedMediaRemainsValid()
    {
        object[] requests =
        [
            new UpdatePostRequest { Content = "post" },
            new UpdateCommentRequest { Content = "comment" },
            new UpdateScheduledPostRequest { Content = "post" }
        ];

        Assert.All(requests, request => Assert.True(Validator.TryValidateObject(
            request, new ValidationContext(request), [], validateAllProperties: true)));
    }
}
