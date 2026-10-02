using Threads.Application.Exceptions;
using Threads.Application.Services.Common;

namespace Threads.Application.UnitTests.Services.Common;

public class SearchQueryNormalizerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Normalize_WhenQueryIsBlank_ReturnsNull(string? query)
    {
        Assert.Null(SearchQueryNormalizer.Normalize(query, 10, "Query"));
    }

    [Fact]
    public void Normalize_WhenQueryIsValid_TrimsValue()
    {
        Assert.Equal("query", SearchQueryNormalizer.Normalize("  query  ", 5, "Query"));
    }

    [Fact]
    public void Normalize_WhenQueryExceedsLimit_ThrowsRequestValidationException()
    {
        var exception = Assert.Throws<RequestValidationException>(() =>
            SearchQueryNormalizer.Normalize("123456", 5, "Search query"));

        Assert.Equal("Search query must contain at most 5 characters.", exception.Message);
    }
}
