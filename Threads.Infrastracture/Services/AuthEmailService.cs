using Microsoft.Extensions.Configuration;
using Resend;
using Threads.Application.Interfaces.Auth;

namespace Threads.Infrastracture.Services;

public class AuthEmailService : IAuthEmailService
{
    private readonly IResend _resend;
    private readonly IConfiguration _configuration;

    public AuthEmailService(IResend resend, IConfiguration configuration)
    {
        _resend = resend;
        _configuration = configuration;
    }

    public async Task SendEmailVerificationCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(
            email,
            "Verify your email",
            $"""
             <div>
                 <p>Your email verification code is <strong>{code}</strong>.</p>
                 <p>This code expires in 15 minutes.</p>
             </div>
             """,
            $"Your email verification code is {code}. This code expires in 15 minutes.");

        await _resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendPasswordResetCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(
            email,
            "Your password reset code",
            $"""
             <div>
                 <p>Your password reset code is <strong>{code}</strong>.</p>
                 <p>This code expires in 15 minutes.</p>
             </div>
             """,
            $"Your password reset code is {code}. This code expires in 15 minutes.");

        await _resend.EmailSendAsync(message, cancellationToken);
    }

    public async Task SendPasswordChangeCodeAsync(
        string email,
        string code,
        CancellationToken cancellationToken = default)
    {
        var message = CreateMessage(
            email,
            "Confirm your password change",
            $"""
             <div>
                 <p>Your password change confirmation code is <strong>{code}</strong>.</p>
                 <p>This code expires in 15 minutes.</p>
             </div>
             """,
            $"Your password change confirmation code is {code}. This code expires in 15 minutes.");

        await _resend.EmailSendAsync(message, cancellationToken);
    }

    private EmailMessage CreateMessage(
        string email,
        string subject,
        string htmlBody,
        string textBody)
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
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody
        };

        message.To.Add(email);

        return message;
    }
}
