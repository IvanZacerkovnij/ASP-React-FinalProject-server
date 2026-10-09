using System.Text.Json;
using System.Text.Json.Serialization;
using Threads.Application.DTOs.LinkPreviews;

namespace Threads.Application.DTOs.ScheduledPosts;

public sealed class UpdateScheduledPostRequestConverter : JsonConverter<UpdateScheduledPostRequest>
{
    public override UpdateScheduledPostRequest Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var values = document.RootElement.Deserialize<PatchValues>(options) ?? throw new JsonException();

        return new UpdateScheduledPostRequest
        {
            Content = values.Content,
            MediaIds = values.MediaIds,
            LinkPreview = values.LinkPreview,
            ScheduledAt = values.ScheduledAt,
            HasContentValue = HasProperty(document.RootElement, nameof(values.Content), options),
            HasLinkPreviewValue = HasProperty(document.RootElement, nameof(values.LinkPreview), options),
            HasScheduledAtValue = HasProperty(document.RootElement, nameof(values.ScheduledAt), options)
        };
    }

    public override void Write(
        Utf8JsonWriter writer, UpdateScheduledPostRequest value, JsonSerializerOptions options)
    {
        JsonSerializer.Serialize(writer, new PatchValues
        {
            Content = value.Content,
            MediaIds = value.MediaIds,
            LinkPreview = value.LinkPreview,
            ScheduledAt = value.ScheduledAt
        }, options);
    }

    private static bool HasProperty(JsonElement element, string name, JsonSerializerOptions options)
    {
        var jsonName = options.PropertyNamingPolicy?.ConvertName(name) ?? name;
        var comparison = options.PropertyNameCaseInsensitive
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return element.EnumerateObject().Any(property => string.Equals(property.Name, jsonName, comparison));
    }

    private sealed class PatchValues
    {
        public string? Content { get; init; }
        public IReadOnlyCollection<Guid>? MediaIds { get; init; }
        public LinkPreviewRequest? LinkPreview { get; init; }
        public DateTimeOffset? ScheduledAt { get; init; }
    }
}
