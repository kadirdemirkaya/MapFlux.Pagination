using System;
using MapFlux.Pagination.Models;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class ValidationTests
{
    [Fact]
    public void PageNumber_LessThanOne_ShouldThrowException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaginationOptions { PageNumber = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaginationOptions { PageNumber = -5 });
    }

    [Fact]
    public void PageSize_LessThanOne_ShouldThrowException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaginationOptions { PageSize = 0 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new PaginationOptions { PageSize = -10 });
    }

    [Fact]
    public void ValidValues_ShouldNotThrowException()
    {
        var opts = new PaginationOptions { PageNumber = 1, PageSize = 1 };
        Assert.Equal(1, opts.PageNumber);
        Assert.Equal(1, opts.PageSize);
    }

    [Fact]
    public void ClampPageSize_ShouldLimitValueCorrectly()
    {
        var opts = new PaginationOptions { PageSize = 50 };
        var clamped = opts.ClampPageSize(25);
        Assert.Equal(25, clamped.PageSize);

        var notClamped = opts.ClampPageSize(100);
        Assert.Equal(50, notClamped.PageSize);
    }

    [Fact]
    public void PagedResultEmpty_ShouldReturnEmptyResult()
    {
        var empty = PagedResult<string>.Empty(15);
        Assert.Empty(empty.Items);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(1, empty.PageNumber);
        Assert.Equal(15, empty.PageSize);
        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasNextPage);
        Assert.False(empty.HasPreviousPage);
        Assert.Equal(0, empty.FirstItemIndex);
        Assert.Equal(0, empty.LastItemIndex);
    }

    [Fact]
    public void ItemIndices_ShouldCalculateCorrectly()
    {
        var items = new[] { "A", "B", "C" };
        var result = new PagedResult<string>(items, 25, 2, 10);
        Assert.Equal(11, result.FirstItemIndex); // (2-1)*10 + 1
        Assert.Equal(20, result.LastItemIndex);  // Math.Min(20, 25)

        var lastPageResult = new PagedResult<string>(items, 25, 3, 10);
        Assert.Equal(21, lastPageResult.FirstItemIndex);
        Assert.Equal(25, lastPageResult.LastItemIndex); // Math.Min(30, 25)
    }
}
