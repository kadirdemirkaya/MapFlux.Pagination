using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class PaginationOptionsTests
{
    [Theory]
    [InlineData(1, 10, 0, 10)]   // Page 1, size 10 => skip 0, take 10
    [InlineData(2, 10, 10, 10)]  // Page 2, size 10 => skip 10, take 10
    [InlineData(3, 25, 50, 25)]  // Page 3, size 25 => skip 50, take 25
    [InlineData(5, 20, 80, 20)]  // Page 5, size 20 => skip 80, take 20
    public void SkipTake_ShouldCalculateCorrectly(int pageNumber, int pageSize, int expectedSkip, int expectedTake)
    {
        // Arrange & Act
        var opts = new PaginationOptions { PageNumber = pageNumber, PageSize = pageSize };

        // Assert
        Assert.Equal(expectedSkip, opts.Skip);
        Assert.Equal(expectedTake, opts.Take);
    }

    [Fact]
    public void DefaultValues_ShouldBeCorrect()
    {
        // Arrange & Act
        var opts = new PaginationOptions();

        // Assert
        Assert.Equal(1, opts.PageNumber);
        Assert.Equal(10, opts.PageSize);
        Assert.Null(opts.SortBy);
        Assert.False(opts.SortDescending);
    }

    [Fact]
    public void WithSortingOptions_ShouldSetCorrectly()
    {
        // Arrange & Act
        var opts = new PaginationOptions
        {
            PageNumber = 1,
            PageSize = 20,
            SortBy = "CreatedAt",
            SortDescending = true
        };

        // Assert
        Assert.Equal("CreatedAt", opts.SortBy);
        Assert.True(opts.SortDescending);
        Assert.Equal(0, opts.Skip);
        Assert.Equal(20, opts.Take);
    }
}
