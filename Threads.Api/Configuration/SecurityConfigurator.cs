using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Threads.Application.Interfaces.Auth;
using Threads.Application.Interfaces.Security;
using Threads.Infrastructure.Security;

using Threads.Infrastructure.Services;

namespace Threads.Api.Configuration;

public static class SecurityConfigurator
{
    public static WebApplicationBuilder AddSecurityServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IAuthCodeHasher, AuthCodeHasher>();
        builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
            new ConfigurationManager<OpenIdConnectConfiguration>(
                "https://accounts.google.com/.well-known/openid-configuration",
                new OpenIdConnectConfigurationRetriever()));
        builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
        builder.Services.AddScoped<IPasswordHasher, PasswordHasher>();
        builder.Services.AddScoped<ITokenService, JwtTokenService>();

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options => JwtBearerConfigurator.Configure(options, builder.Configuration));

        builder.Services.AddAuthorization(AuthorizationConfigurator.Configure);

        builder.Services.AddCors(options => CORSConfigurator.Configure(options, builder.Configuration));

        return builder;
    }
}
