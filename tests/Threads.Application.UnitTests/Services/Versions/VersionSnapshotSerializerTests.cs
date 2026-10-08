using Threads.Application.Services.Versions;
using Threads.Domain.Enums;
using Threads.Domain.Models.Versions;

namespace Threads.Application.UnitTests.Services.Versions;

public sealed class VersionSnapshotSerializerTests
{
    private readonly VersionSnapshotSerializer _serializer = new();

    [Fact]
    public void SerializeAndDeserialize_PostSnapshot_RoundTrips()
    {
        var targetId = Guid.NewGuid();
        var targetVersionId = Guid.NewGuid();

        var snapshot = new PostVersionSnapshot
        {
            Content = "Quoted post",
            MediaIds = [Guid.NewGuid()],
            Quote = new QuoteVersionSnapshot
            {
                TargetType = ContentTargetType.Comment,
                TargetId = targetId,
                TargetVersionId = targetVersionId
            }
        };

        var json = _serializer.Serialize(snapshot);
        var result = _serializer.Deserialize<PostVersionSnapshot>(json);

        Assert.Equal(snapshot.Content, result.Content);
        Assert.Equal(snapshot.MediaIds, result.MediaIds);
        Assert.NotNull(result.Quote);
        Assert.Equal(ContentTargetType.Comment, result.Quote.TargetType);
        Assert.Equal(targetId, result.Quote.TargetId);
        Assert.Equal(targetVersionId, result.Quote.TargetVersionId);
        Assert.Contains("\"targetType\":\"comment\"", json);
    }
}