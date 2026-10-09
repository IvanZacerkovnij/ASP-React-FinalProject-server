using System.Text.Json;
using Threads.Application.DTOs.ScheduledPosts;

namespace Threads.Application.UnitTests.DTOs.Validation;

public sealed class UpdateScheduledPostRequestTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData("{}", false, false, false)]
    [InlineData("{\"CoNtEnT\":null}", true, false, false)]
    [InlineData("{\"hasContentValue\":true}", false, false, false)]
    [InlineData("{\"content\":null}", true, false, false)]
    [InlineData("{\"content\":\"updated\"}", true, false, false)]
    [InlineData("{\"linkPreview\":null}", false, true, false)]
    [InlineData("{\"linkPreview\":{\"url\":\"https://example.com\"}}", false, true, false)]
    [InlineData("{\"scheduledAt\":null}", false, false, true)]
    [InlineData("{\"scheduledAt\":\"2099-01-01T12:00:00Z\"}", false, false, true)]
    public void Deserialize_DistinguishesOmittedFieldsFromExplicitNullAndValues(
        string json, bool hasContent, bool hasLinkPreview, bool hasScheduledAt)
    {
        var request = JsonSerializer.Deserialize<UpdateScheduledPostRequest>(json, JsonOptions)!;

        Assert.Equal(hasContent, request.HasContentValue);
        Assert.Equal(hasLinkPreview, request.HasLinkPreviewValue);
        Assert.Equal(hasScheduledAt, request.HasScheduledAtValue);
        using var source = JsonDocument.Parse(json);
        if (source.RootElement.TryGetProperty("content", out var content))
        {
            Assert.Equal(content.GetString(), request.Content);
        }
        if (source.RootElement.TryGetProperty("linkPreview", out var linkPreview))
        {
            Assert.Equal(linkPreview.ValueKind == JsonValueKind.Null ? null : "https://example.com", request.LinkPreview?.Url);
        }
        if (source.RootElement.TryGetProperty("scheduledAt", out var scheduledAt))
        {
            Assert.Equal(scheduledAt.ValueKind == JsonValueKind.Null ? (DateTimeOffset?)null : scheduledAt.GetDateTimeOffset(), request.ScheduledAt);
        }

        using var serialized = JsonDocument.Parse(JsonSerializer.Serialize(request, JsonOptions));
        Assert.False(serialized.RootElement.TryGetProperty("hasContentValue", out _));
        Assert.False(serialized.RootElement.TryGetProperty("hasLinkPreviewValue", out _));
        Assert.False(serialized.RootElement.TryGetProperty("hasScheduledAtValue", out _));
    }

    [Fact]
    public void Deserialize_PreservesMediaIdsAndRejectsInvalidFieldTypes()
    {
        var mediaId = Guid.NewGuid();
        var request = JsonSerializer.Deserialize<UpdateScheduledPostRequest>(
            $"{{\"mediaIds\":[\"{mediaId}\"]}}", JsonOptions)!;

        Assert.Equal(new[] { mediaId }, request.MediaIds);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateScheduledPostRequest>(
            "{\"content\":123}", JsonOptions));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<UpdateScheduledPostRequest>(
            "{\"scheduledAt\":\"not-a-date\"}", JsonOptions));
    }
}
