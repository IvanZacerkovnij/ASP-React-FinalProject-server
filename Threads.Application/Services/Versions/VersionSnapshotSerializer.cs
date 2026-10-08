using System.Text.Json;
using System.Text.Json.Serialization;
using Threads.Application.Interfaces.Versions;

namespace Threads.Application.Services.Versions;

public sealed class VersionSnapshotSerializer : IVersionSnapshotSerializer
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

    public string Serialize<TSnapshot>(TSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        
        return JsonSerializer.Serialize(snapshot, typeof(TSnapshot), SerializerOptions);
    }

    public TSnapshot Deserialize<TSnapshot>(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new ArgumentException(
                $"'{nameof(json)}' cannot be null or whitespace.",
                nameof(json));
        }
        return JsonSerializer.Deserialize<TSnapshot>(
            json,
            SerializerOptions) ??
               throw new JsonException($"Unable to deserialize '{typeof(TSnapshot).Name}'.");
    }
}