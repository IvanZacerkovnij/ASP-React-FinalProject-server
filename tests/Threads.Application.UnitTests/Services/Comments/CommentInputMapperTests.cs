using Threads.Application.DTOs.Comments;
using Threads.Application.DTOs.LinkPreviews;
using Threads.Application.DTOs.Posts.Requests;
using Threads.Application.Exceptions;
using Threads.Application.Services.Comments;
using Threads.Domain.Entities;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentInputMapperTests
{
    [Fact]
    public void ApplyMetadataChanges_WhenRemovalAndValueAreProvided_ThrowsRequestValidationException()
    {
        var comment = new Comment { Content = "comment" };

        var pollException = Assert.Throws<RequestValidationException>(() =>
            CommentInputMapper.ApplyMetadataChanges(comment, new UpdateCommentRequest
            {
                Content = "updated",
                RemovePoll = true,
                Poll = new CreatePostPollRequest { Options = ["First", "Second"] }
            }));
        var locationException = Assert.Throws<RequestValidationException>(() =>
            CommentInputMapper.ApplyMetadataChanges(comment, new UpdateCommentRequest
            {
                Content = "updated",
                RemoveLocation = true,
                Location = new PostLocationRequest { Name = "Kyiv" }
            }));
        var linkPreviewException = Assert.Throws<RequestValidationException>(() =>
            CommentInputMapper.ApplyMetadataChanges(comment, new UpdateCommentRequest
            {
                Content = "updated",
                RemoveLinkPreview = true,
                LinkPreview = new LinkPreviewRequest { Url = "https://example.com" }
            }));

        Assert.Equal(
            "Poll cannot be provided when poll removal is requested.",
            pollException.Message);
        Assert.Equal(
            "Location cannot be provided when location removal is requested.",
            locationException.Message);
        Assert.Equal(
            "Link preview cannot be provided when link preview removal is requested.",
            linkPreviewException.Message);
    }
}
