using Threads.Application.DTOs.Validation;

namespace Threads.Application.UnitTests.DTOs.Validation;

public class NotInFutureDateAttributeTest
{
    private readonly NotInFutureDateAttribute _attribute = new();

    [Theory]
    [InlineData(10, false)]
    [InlineData(-10, true)]
    public void IsValid_WhenHasMinuteOffset_ReturnsExpectedResult(
        int daysOffset,
        bool expectedResult)
    {
        var date = DateOnly.FromDateTime(DateTime.Now.AddDays(daysOffset));
        
        var result =  _attribute.IsValid(date);
        
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