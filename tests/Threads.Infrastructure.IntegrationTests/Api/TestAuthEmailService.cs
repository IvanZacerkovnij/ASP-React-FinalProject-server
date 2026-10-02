using System.Collections.Concurrent;
using Threads.Application.Interfaces.Auth;

namespace Threads.Infrastructure.IntegrationTests.Api;

internal sealed class TestAuthEmailService : IAuthEmailService
{
    private readonly ConcurrentDictionary<string, string> _verificationCodes = new();
    private readonly ConcurrentDictionary<string, string> _passwordResetCodes = new();
    private readonly ConcurrentDictionary<string, string> _passwordChangeCodes = new();

    public string GetVerificationCode(string email)
    {
        return _verificationCodes[email];
    }

    public Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _verificationCodes[email] = code;
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _passwordResetCodes[email] = code;
        return Task.CompletedTask;
    }

    public Task SendPasswordChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        _passwordChangeCodes[email] = code;
        return Task.CompletedTask;
    }
}
