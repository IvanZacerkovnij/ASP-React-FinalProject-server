using Microsoft.Extensions.Configuration;
using Resend;
using Threads.Application.Interfaces.Auth;

namespace Threads.Infrastracture.Services;

public class ResendPasswordResetEmailService : IPasswordResetEmailService
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;

    public ResendPasswordResetEmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _configuration = configuration;
    }

    public async Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var fromEmail = _configuration["RESEND_FROM_EMAIL"];

        if (string.IsNullOrWhiteSpace(fromEmail))
        {
            throw new InvalidOperationException("RESEND_FROM_EMAIL is not configured.");
        }

        var fromName = _configuration["RESEND_FROM_NAME"];
        var message = new EmailMessage
        {
            From = string.IsNullOrWhiteSpace(fromName)
                ? fromEmail
                : $"{fromName} <{fromEmail}>",
            Subject = "Your password reset code",
            HtmlBody = $"""
                <div>
                    <p>Your password reset code is <strong>{code}</strong>.</p>
                    <p>This code expires in 15 minutes.</p>
                </div>
                """,
            TextBody = $"Your password reset code is {code}. This code expires in 15 minutes."
        };

        message.To.Add(email);

        await _resend.EmailSendAsync(message, cancellationToken);
    }
}
