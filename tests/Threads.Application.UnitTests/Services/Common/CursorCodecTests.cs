using Threads.Application.Exceptions;
using Threads.Application.Services.Common;

namespace Threads.Application.UnitTests.Services.Common;

public class CursorCodecTests
{
    [Fact]
    public void EncodeAndDecode_RoundTripCursorPosition()
    {
        var createdAt = DateTimeOffset.UtcNow;
        var id = Guid.NewGuid();

        var cursor = CursorCodec.Encode(createdAt, id);
        var result = CursorCodec.Decode(cursor);

        Assert.NotNull(result);
        Assert.Equal(createdAt, result.CreatedAt);
        Assert.Equal(id, result.Id);
        Assert.DoesNotContain('=', cursor);
    }

    [Fact]
    public void EncodeTextAndDecodeText_RoundTripUnicodeValue()
    {
        const string value = "Іван | test";
        var id = Guid.NewGuid();

        var cursor = CursorCodec.EncodeText(value, id);
        var result = CursorCodec.DecodeText(cursor);

        Assert.NotNull(result);
        Assert.Equal(value, result.Value);
        Assert.Equal(id, result.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Decode_WhenCursorIsBlank_ReturnsNull(string? cursor)
    {
        Assert.Null(CursorCodec.Decode(cursor));
        Assert.Null(CursorCodec.DecodeText(cursor));
    }

    [Theory]
    [InlineData("not-base64")]
    [InlineData("YWJj")]
    public void Decode_WhenCursorIsInvalid_ThrowsRequestValidationException(string cursor)
    {
        Assert.Throws<RequestValidationException>(() => CursorCodec.Decode(cursor));
        Assert.Throws<RequestValidationException>(() => CursorCodec.DecodeText(cursor));
    }
}
