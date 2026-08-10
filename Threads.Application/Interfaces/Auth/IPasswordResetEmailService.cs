namespace Threads.Application.Interfaces.Auth;

public interface IPasswordResetEmailService
{
    Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);

    Task SendPasswordChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default);
}
