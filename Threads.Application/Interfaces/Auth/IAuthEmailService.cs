namespace Threads.Application.Interfaces.Auth;

public interface IAuthEmailService
{
    Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task SendPasswordChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);
}
