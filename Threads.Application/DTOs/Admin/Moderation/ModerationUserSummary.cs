namespace Threads.Application.DTOs.Admin;

public sealed class ModerationUserSummary
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
}
