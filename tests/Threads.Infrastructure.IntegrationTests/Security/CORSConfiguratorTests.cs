using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class CORSConfiguratorTests
{
    [Theory]
    [InlineData("https://chirp-seven-sigma.vercel.app", true)]
    [InlineData("http://chirp-seven-sigma.vercel.app", false)]
    [InlineData("https://chirp-seven-sigma.vercel.app.attacker.example", false)]
    [InlineData("http://localhost:8000", false)]
    public void Configure_Preflight_AllowsOnlyConfiguredOrigin(string origin, bool expectedAllowed)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "https://chirp-seven-sigma.vercel.app"
        }).Build();
        var options = new CorsOptions();
        CORSConfigurator.Configure(options, configuration);
        var policy = options.GetPolicy("AllowAll");
        Assert.NotNull(policy);
        var context = new DefaultHttpContext();
        context.Request.Method = "OPTIONS";
        context.Request.Headers.Origin = origin;
        context.Request.Headers.AccessControlRequestMethod = "POST";
        context.Request.Headers.AccessControlRequestHeaders = "authorization,content-type";
        var service = new CorsService(Options.Create(options), NullLoggerFactory.Instance);

        var result = service.EvaluatePolicy(context, policy);
        service.ApplyResult(result, context.Response);

        Assert.Equal(expectedAllowed, result.IsOriginAllowed);
        if (expectedAllowed)
        {
            Assert.Equal(origin, context.Response.Headers.AccessControlAllowOrigin.ToString());
            Assert.Contains("POST", result.AllowedMethods);
            Assert.Contains("authorization", result.AllowedHeaders);
            Assert.Contains("content-type", result.AllowedHeaders);
        }
        else
        {
            Assert.False(context.Response.Headers.ContainsKey("Access-Control-Allow-Origin"));
        }
    }
}
