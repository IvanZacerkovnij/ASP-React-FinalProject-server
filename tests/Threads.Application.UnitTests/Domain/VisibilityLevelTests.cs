using System.ComponentModel;
using System.Text.Json;
using Threads.Domain.Enums;

namespace Threads.Application.UnitTests.Domain;

public class VisibilityLevelTests
{
    [Theory]
    [InlineData(VisibilityLevel.Public, "\"public\"")]
    [InlineData(VisibilityLevel.Followers, "\"followers\"")]
    [InlineData(VisibilityLevel.Following, "\"following\"")]
    [InlineData(VisibilityLevel.Mutual, "\"mutual\"")]
    [InlineData(VisibilityLevel.OnlyMe, "\"only_me\"")]
    public void Serialize_UsesClientContractNames(VisibilityLevel level, string expectedJson)
    {
        var json = JsonSerializer.Serialize(level);

        Assert.Equal(expectedJson, json);
    }

    [Theory]
    [InlineData("public", VisibilityLevel.Public)]
    [InlineData("followers", VisibilityLevel.Followers)]
    [InlineData("following", VisibilityLevel.Following)]
    [InlineData("mutual", VisibilityLevel.Mutual)]
    [InlineData("only_me", VisibilityLevel.OnlyMe)]
    public void TypeConverter_ParsesClientContractNames(string value, VisibilityLevel expected)
    {
        var converter = TypeDescriptor.GetConverter(typeof(VisibilityLevel?));

        var result = converter.ConvertFrom(value);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("OnlyMe")]
    [InlineData("4")]
    [InlineData("everyone")]
    public void TypeConverter_WhenValueIsNotContractName_Throws(string value)
    {
        var converter = TypeDescriptor.GetConverter(typeof(VisibilityLevel));

        Assert.Throws<FormatException>(() => converter.ConvertFrom(value));
    }
}
