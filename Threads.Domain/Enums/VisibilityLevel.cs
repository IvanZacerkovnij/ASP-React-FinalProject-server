using System.ComponentModel;
using System.Text.Json.Serialization;
using Threads.Domain.Converter;

namespace Threads.Domain.Enums;

[JsonConverter(typeof(JsonStringEnumConverter<VisibilityLevel>))]
[TypeConverter(typeof(VisibilityLevelConverter))]
public enum VisibilityLevel
{
    [JsonStringEnumMemberName("public")]
    Public,

    [JsonStringEnumMemberName("followers")]
    Followers,

    [JsonStringEnumMemberName("following")]
    Following,

    [JsonStringEnumMemberName("mutual")]
    Mutual,

    [JsonStringEnumMemberName("only_me")]
    OnlyMe
}
