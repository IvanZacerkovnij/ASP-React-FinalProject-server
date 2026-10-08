using Threads.Application.Services.Comments;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Comments;

public sealed class CommentVersionFactoryTests
{
    private static VersionSnapshotSerializer serializer =
        new VersionSnapshotSerializer();
    private CommentVersionFactory factory =
        new CommentVersionFactory(serializer);
    
    [Fact]
    public void Create_UsesCurrentVersionIdAndSerializeContent()
    {
        var versionId = Guid.NewGuid();
        var comment = new Comment
        {
            Content = "Initial comment",
            CurrentVersionId = versionId,
            LinkPreviewUrl = "https://example.com/article",
            LinkPreviewTitle = "Article",
            LinkPreviewImageUrl = "https://example.com/image.jpg"
        };
        comment.LocationName = "Kyiv";
        comment.Poll = new Poll
        {
            Options =
            [
                new PollOption { Text = "First", Position = 0 },
                new PollOption { Text = "Second", Position = 1 }
            ]
        };

        var result = factory.Create(comment);
        var snapshot = serializer.Deserialize<CommentVersionSnapshot>(
            result.SnapshotJson);

        Assert.Equal(versionId, result.Id);
        Assert.Equal(comment.Id, result.CommentId);
        Assert.Same(comment, result.Comment);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("Initial comment", snapshot.Content);
        Assert.Equal(comment.LinkPreviewUrl, snapshot.LinkPreview?.Url);
        Assert.Equal(comment.LinkPreviewTitle, snapshot.LinkPreview?.Title);
        Assert.Equal(comment.LinkPreviewImageUrl, snapshot.LinkPreview?.ThumbnailUrl);
        Assert.Equal("Kyiv", snapshot.Location?.Name);
        Assert.Equal(["First", "Second"], snapshot.Poll?.Options.Select(option => option.Text));
    }
}
