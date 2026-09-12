using Microsoft.AspNetCore.Authorization;
using Threads.Domain.Enums;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.Services;

public static class AuthorizationConfigurator
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(
            AuthorizationPolicies.Moderation,
            policy => policy.RequireRole(nameof(UserRole.Moderator)));
    }
}
