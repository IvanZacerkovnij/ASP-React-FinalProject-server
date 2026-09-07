using Microsoft.AspNetCore.Authorization;
using Threads.Domain.Enums;
using Threads.Infrastracture.Security;

namespace Threads.Infrastracture.Services;

public static class AuthorizationConfigurator
{
    public static void Configure(AuthorizationOptions options)
    {
        options.AddPolicy(
            AuthorizationPolicies.Moderation,
            policy => policy.RequireRole(nameof(UserRole.Moderator)));
    }
}