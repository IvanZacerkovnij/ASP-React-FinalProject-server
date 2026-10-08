using Threads.Application.Services.Posts;
using Threads.Application.Services.Versions;
using Threads.Domain.Entities;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Posts;

public sealed class PostVersionFactoryTests
{
    private static VersionSnapshotSerializer serializer =
        new VersionSnapshotSerializer();
    private PostVersionFactory factory =
        new PostVersionFactory(serializer);
    
    [Fact]
    public void Create_UsesCurrentVersionIdAndSerializeContent()
    {
        var versionId = Guid.NewGuid();
        var post = new Post
        {
            Content = "Initial content",
            CurrentVersionId = versionId,
        };
        
        var result = factory.Create(post);
        var snapshot = serializer.Deserialize<PostVersionSnapshot>(
            result.SnapshotJson);
        
        Assert.Equal(versionId, result.Id);
        Assert.Equal(post.Id, result.PostId);
        Assert.Same(post, result.Post);
        Assert.Equal(1, result.SchemaVersion);
        Assert.Equal("Initial content", snapshot.Content);
    }
}