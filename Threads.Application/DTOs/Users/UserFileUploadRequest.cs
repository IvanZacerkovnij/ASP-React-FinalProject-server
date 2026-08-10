namespace Threads.Application.DTOs.Users;

public sealed class UserFileUploadRequest
{
    public required Stream Content { get; init; }

    public required string FileName { get; init; }

    public required string ContentType { get; init; }

    public long SizeInBytes { get; init; }
}
