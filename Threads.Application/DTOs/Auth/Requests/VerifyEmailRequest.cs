namespace Threads.Application.DTOs.Auth.Requests;

public class VerifyEmailRequest
{
    public required string Email { get; init; }
    public required string Code {get; init;}
}