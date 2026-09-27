using MapFlux.Pagination.Models;
using System.Collections.Generic;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class PagedResultTests
{
    [Fact]
    public void PagedResult_ShouldCalculate_TotalPagesCorrectly()
    {
        // Arrange
        var items = new List<int> { 1, 2, 3 };
        var totalCount = 10;
        var pageNumber = 1;
        var pageSize = 3;

        // Act
        var result = new PagedResult<int>(items, totalCount, pageNumber, pageSize);

        // Assert
        Assert.Equal(4, result.TotalPages); // 10 / 3 = 3.33 -> 4
        Assert.True(result.HasNextPage);
        Assert.False(result.HasPreviousPage);
    }

    [Fact]
    public void PagedResult_ShouldCalculate_NextAndPreviousCorrectly_LastPage()
    {
        // Arrange
        var items = new List<int> { 10 };
        var totalCount = 10;
        var pageNumber = 4;
        var pageSize = 3;

        // Act
        var result = new PagedResult<int>(items, totalCount, pageNumber, pageSize);

        // Assert
        Assert.Equal(4, result.TotalPages);
        Assert.False(result.HasNextPage);
        Assert.True(result.HasPreviousPage);
    }

    [Fact]
    public void PagedResult_ShouldReturn_ZeroTotalPages_WhenPageSizeIsZero()
    {
        // Arrange
        var items = new List<int>();
        var totalCount = 5;
        var pageNumber = 1;
        var pageSize = 0;

        // Act
        var result = new PagedResult<int>(items, totalCount, pageNumber, pageSize);

        // Assert
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void PagedResult_ShouldReturn_ZeroTotalPages_WhenPageSizeIsNegative()
    {
        // Arrange
        var items = new List<int>();
        var totalCount = 5;
        var pageNumber = 1;
        var pageSize = -3;

        // Act
        var result = new PagedResult<int>(items, totalCount, pageNumber, pageSize);

        // Assert
        Assert.Equal(0, result.TotalPages);
        Assert.False(result.HasNextPage);
    }
}
