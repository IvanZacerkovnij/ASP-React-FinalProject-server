using System.Text.Json.Serialization;

namespace Threads.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<ContentTargetType>))]
public enum ContentTargetType
{
    [JsonStringEnumMemberName("post")]
    Post,
    [JsonStringEnumMemberName("comment")]
    Comment
}