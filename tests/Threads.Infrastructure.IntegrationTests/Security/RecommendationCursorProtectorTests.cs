using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Threads.Application.Exceptions;
using Threads.Application.Recommendations;
using Threads.Infrastructure.Security;

namespace Threads.Infrastructure.IntegrationTests.Security;

public sealed class RecommendationCursorProtectorTests
{
    [Fact]
    public void Protect_MaximumSnapshotFitsHttpRequestLineAndRoundTrips()
    {
        var protector = new RecommendationCursorProtector(new EphemeralDataProtectionProvider());
        var snapshot = new RecommendationSnapshot(Guid.NewGuid(), "posts", DateTimeOffset.UtcNow.AddMinutes(30),
            Enumerable.Range(0, RecommendationOptions.MaximumResultLimit).Select(_ => Guid.NewGuid()).ToArray());
        var json = JsonSerializer.Serialize(snapshot);

        var cursor = protector.Protect(json);

        Assert.True(cursor.Length < 7_500);
        Assert.Equal(json, protector.Unprotect(cursor));
        Assert.DoesNotContain('+', cursor);
        Assert.DoesNotContain('/', cursor);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("!not-base64!")]
    public void Unprotect_InvalidCursorReturnsValidationError(string cursor)
    {
        var protector = new RecommendationCursorProtector(new EphemeralDataProtectionProvider());

        Assert.Throws<RequestValidationException>(() => protector.Unprotect(cursor));
    }

    [Fact]
    public void Unprotect_RejectsTamperedPayloadAndDifferentKeys()
    {
        var protector = new RecommendationCursorProtector(new EphemeralDataProtectionProvider());
        var otherProtector = new RecommendationCursorProtector(new EphemeralDataProtectionProvider());
        var cursor = protector.Protect("snapshot");
        var middle = cursor.Length / 2;
        var tampered = cursor[..middle] + (cursor[middle] == 'A' ? 'B' : 'A') + cursor[(middle + 1)..];

        Assert.Throws<RequestValidationException>(() => protector.Unprotect(tampered));
        Assert.Throws<RequestValidationException>(() => otherProtector.Unprotect(cursor));
    }
}
