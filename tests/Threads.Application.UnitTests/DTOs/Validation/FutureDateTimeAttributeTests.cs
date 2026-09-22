using Threads.Application.DTOs.Validation;

namespace Threads.Application.UnitTests.DTOs.Validation;

public class FutureDateTimeAttributeTests
{
    private readonly FutureDateTimeAttribute _attribute = new();

    [Theory]
    [InlineData(10, true)]
    [InlineData(-10, false)]
    public void IsValid_WhenDateHasMinuteOffset_ReturnsExpectedResult(
        int minuteOffset,
        bool expectedResult)
    {
        var date = DateTimeOffset.UtcNow.AddMinutes(minuteOffset);

        var result = _attribute.IsValid(date);

        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void IsValid_WhenValueIsNull_ReturnsTrue()
    {
        var result = _attribute.IsValid(null);

        Assert.True(result);
    }

    [Fact]
    public void IsValid_WhenValueHasWrongType_ReturnsFalse()
    {
        var result = _attribute.IsValid("not a date");

        Assert.False(result);
    }
}
