using MapFlux;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace MapFlux.Pagination.Tests;

public class PaginatedMapperTests
{
    private class User { public int Id { get; set; } public string Name { get; set; } = string.Empty; }
    private class UserDto { public int Id { get; set; } public string FullName { get; set; } = string.Empty; }

    private class UserProfile : Profile
    {
        public override void Configure(IMapperConfigurationExpression cfg)
        {
            cfg.CreateMap<User, UserDto>(opt => opt.ForMember(d => d.FullName, m => m.MapFrom(s => s.Name)));
        }
    }

    private class UserDbContext : DbContext
    {
        public DbSet<User> Users => Set<User>();

        protected override void OnConfiguring(DbContextOptionsBuilder options)
            => options.UseInMemoryDatabase("paginatedmapper_" + System.Guid.NewGuid());
    }

    [Fact]
    public void MapPaged_ShouldMap_ItemsCorrectly()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<UserProfile>();

        var paginatedMapper = new PaginatedMapper<User, UserDto>(mapper);
        
        var sourceItems = new List<User> 
        { 
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };
        var sourcePaged = new PagedResult<User>(sourceItems, 10, 1, 2);

        // Act
        var result = paginatedMapper.MapPaged(sourcePaged);

        // Assert
        Assert.Equal(2, result.Items.Count);
        Assert.Equal("Alice", result.Items[0].FullName);
        Assert.Equal("Bob", result.Items[1].FullName);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(5, result.TotalPages);
    }

    [Fact]
    public void QueryableHelper_ShouldApply_SortingCorrectly()
    {
        // Arrange
        var sourceItems = new List<User> 
        { 
            new() { Id = 1, Name = "Zebra" },
            new() { Id = 2, Name = "Apple" }
        }.AsQueryable();

        var opts = new PaginationOptions 
        { 
            SortBy = "Name",
            SortDescending = false
        };

        // Act
        var query = QueryableHelper.ApplySorting(sourceItems, opts.SortBy, opts.SortDescending);
        var result = query.ToList();

        // Assert
        Assert.Equal("Apple", result[0].Name);
        Assert.Equal("Zebra", result[1].Name);
    }

    [Fact]
    public void QueryableHelper_ShouldApply_DescendingSortingCorrectly()
    {
        // Arrange
        var sourceItems = new List<User> 
        { 
            new() { Id = 1, Name = "Apple" },
            new() { Id = 2, Name = "Zebra" }
        }.AsQueryable();

        var opts = new PaginationOptions 
        { 
            SortBy = "Name",
            SortDescending = true
        };

        // Act
        var query = QueryableHelper.ApplySorting(sourceItems, opts.SortBy, opts.SortDescending);
        var result = query.ToList();

        // Assert
        Assert.Equal("Zebra", result[0].Name);
        Assert.Equal("Apple", result[1].Name);
    }

    [Fact]
    public async Task MapPagedAsync_WithGlobalMaxPageSize_ShouldClampRequestedPageSize()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<UserProfile>();

        var globalOptions = new PaginationGlobalOptions { MaxPageSize = 5 };
        var paginatedMapper = new PaginatedMapper<User, UserDto>(mapper, globalOptions);

        using var context = new UserDbContext();
        context.Users.AddRange(Enumerable.Range(1, 20).Select(i => new User { Id = i, Name = $"User{i}" }));
        await context.SaveChangesAsync();

        var opts = new PaginationOptions { PageNumber = 1, PageSize = 1000 };

        // Act
        var result = await paginatedMapper.MapPagedAsync(context.Users.AsQueryable(), opts);

        // Assert
        Assert.Equal(5, result.PageSize);
        Assert.Equal(5, result.Items.Count);
        Assert.Equal(20, result.TotalCount);
    }

    [Fact]
    public async Task MapPagedAsync_WithoutGlobalOptions_ShouldNotClampPageSize()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<UserProfile>();

        var paginatedMapper = new PaginatedMapper<User, UserDto>(mapper);

        using var context = new UserDbContext();
        context.Users.AddRange(Enumerable.Range(1, 20).Select(i => new User { Id = i, Name = $"User{i}" }));
        await context.SaveChangesAsync();

        var opts = new PaginationOptions { PageNumber = 1, PageSize = 1000 };

        // Act
        var result = await paginatedMapper.MapPagedAsync(context.Users.AsQueryable(), opts);

        // Assert
        Assert.Equal(1000, result.PageSize);
        Assert.Equal(20, result.Items.Count);
    }

    [Fact]
    public async Task MapCursorPagedAsync_WithGlobalMaxPageSize_ShouldClampRequestedPageSize()
    {
        // Arrange
        var mapper = new Mapper();
        mapper.CreateMap<UserProfile>();

        var globalOptions = new PaginationGlobalOptions { MaxPageSize = 5 };
        var paginatedMapper = new PaginatedMapper<User, UserDto>(mapper, globalOptions);

        using var context = new UserDbContext();
        context.Users.AddRange(Enumerable.Range(1, 20).Select(i => new User { Id = i, Name = $"User{i}" }));
        await context.SaveChangesAsync();

        var opts = new CursorPaginationOptions { PageSize = 1000, SortBy = "Id" };

        // Act
        var result = await paginatedMapper.MapCursorPagedAsync(context.Users.AsQueryable(), opts);

        // Assert
        Assert.Equal(5, result.Items.Count);
    }
}
