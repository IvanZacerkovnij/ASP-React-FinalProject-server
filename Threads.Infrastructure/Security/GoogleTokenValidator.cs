using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using Threads.Application.Exceptions;
using Threads.Application.Interfaces.Auth;
using Threads.Infrastructure.Exceptions;

namespace Threads.Infrastructure.Security;

public sealed class GoogleTokenValidator(
    IConfiguration configuration,
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager) : IGoogleTokenValidator
{
    public async Task<GoogleIdentity?> ValidateAsync(
        string idToken,
        CancellationToken cancellationToken = default)
    {
        var clientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(clientId))
        {
            throw new InfrastructureConfigurationException("Google:ClientId");
        }

        if (string.IsNullOrWhiteSpace(idToken) || idToken.Length > 8192)
        {
            return null;
        }

        var handler = new JsonWebTokenHandler { MapInboundClaims = false };
        if (!handler.CanReadToken(idToken))
        {
            return null;
        }

        OpenIdConnectConfiguration googleConfiguration;
        try
        {
            googleConfiguration = await configurationManager.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new ExternalServiceException("Unable to load Google signing keys.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuers = ["https://accounts.google.com", "accounts.google.com"],
            ValidateAudience = true,
            ValidAudience = clientId,
            IgnoreTrailingSlashWhenValidatingAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = googleConfiguration.SigningKeys,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256]
        };
        var result = await handler.ValidateTokenAsync(idToken, parameters).WaitAsync(cancellationToken);
        if (!result.IsValid)
        {
            if (result.Exception is SecurityTokenSignatureKeyNotFoundException)
            {
                configurationManager.RequestRefresh();
            }

            return null;
        }

        var claims = result.ClaimsIdentity;
        var subject = claims.FindFirst("sub")?.Value;
        var email = claims.FindFirst("email")?.Value;
        var authorizedParty = claims.FindFirst("azp")?.Value;
        if (string.IsNullOrWhiteSpace(subject) || subject.Length > 255 ||
            string.IsNullOrWhiteSpace(email) || email.Length > 255 ||
            !bool.TryParse(claims.FindFirst("email_verified")?.Value, out var emailVerified) || !emailVerified ||
            (authorizedParty is not null && authorizedParty != clientId))
        {
            return null;
        }

        var isEmailAuthoritative = email.EndsWith("@gmail.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrWhiteSpace(claims.FindFirst("hd")?.Value);
        return new GoogleIdentity(subject, email, claims.FindFirst("name")?.Value, isEmailAuthoritative);
    }
}
