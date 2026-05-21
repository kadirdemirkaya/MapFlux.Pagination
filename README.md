# MapFlux.Pagination

Advanced Pagination + Object Mapping library for .NET 6.0, 7.0, 8.0, and 9.0, powered by [MapFlux](https://github.com/kadirdemirkaya/MapFlux) and Entity Framework Core.

MapFlux.Pagination allows you to query, filter, sort, and paginate database entities, and automatically map them directly to DTOs in a single database roundtrip.

---

## Features

- ⚡ **Automatic Mapping**: Maps database entities directly to DTOs using MapFlux.
- 🔍 **Dynamic Filtering**: Property-based filtering with support for operators like Equals, Contains, GreaterThan, etc.
- 🔀 **Multi-Column Sorting**: Order by multiple fields dynamically.
- 🔎 **Global Search**: Search across multiple string properties simultaneously.
- ⏳ **Cursor-Based Pagination**: High-performance keyset pagination using base64 encoded cursors.
- 💾 **In-Memory Pagination**: Support for pagination on standard in-memory lists/collections.
- 🛡️ **Input Validation & Safety**: Guardrails against invalid page arguments.

---

## Installation

Install the package via .NET CLI:

```bash
dotnet add package MapFlux.Pagination
```

---

## Quick Start

### 1. Register in `Program.cs`

Register MapFlux Pagination with your mapping profiles:

```csharp
using MapFlux.Pagination.Extensions;

builder.Services.AddMapFluxPagination(cfg => 
{
    cfg.AddProfile<UserProfile>();
});
```

You can optionally configure global default and maximum page limits:

```csharp
builder.Services.AddMapFluxPagination(
    cfg => cfg.AddProfile<UserProfile>(),
    opts => 
    {
        opts.DefaultPageSize = 20;
        opts.MaxPageSize = 100;
    });
```

### 2. Inject and Use in Controllers

Inject `IPaginatedMapper<TSource, TDest>` into your Controller/Service:

```csharp
using Microsoft.AspNetCore.Mvc;
using MapFlux.Pagination.Abstractions;
using MapFlux.Pagination.Models;

[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly IPaginatedMapper<User, UserDto> _mapper;
    private readonly AppDbContext _db;

    public UsersController(IPaginatedMapper<User, UserDto> mapper, AppDbContext db)
    {
        _mapper = mapper;
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1, [FromQuery] int size = 10)
    {
        var opts = new PaginationOptions { PageNumber = page, PageSize = size };
        
        // Applies pagination and maps entities to DTOs in one database query
        IPagedResult<UserDto> result = await _mapper.MapPagedAsync(_db.Users, opts);
        
        return Ok(result);
    }
}
```

---

## Key Usage Scenarios

### Dynamic Filtering & Multi-Sorting

Apply filters and sorting on the fly using `PaginationOptions`:

```csharp
var opts = new PaginationOptions
{
    PageNumber = 1,
    PageSize = 10,
    Filters = new List<FilterCriteria>
    {
        new() { PropertyName = "IsActive", Operator = FilterOperator.Equals, Value = true },
        new() { PropertyName = "Age", Operator = FilterOperator.GreaterThanOrEqual, Value = 18 }
    },
    SortCriterias = new List<SortCriteria>
    {
        new() { PropertyName = "LastName", Descending = false },
        new() { PropertyName = "CreatedAt", Descending = true }
    }
};

var pagedUsers = await _mapper.MapPagedAsync(_db.Users, opts);
```

### Cursor-Based Pagination (Keyset Pagination)

For high-performance pagination on large datasets:

```csharp
[HttpGet("cursor")]
public async Task<IActionResult> GetUsersCursor([FromQuery] string? after, [FromQuery] int size = 20)
{
    var opts = new CursorPaginationOptions 
    { 
        PageSize = size,
        After = after, // Pass the previous page's EndCursor
        CursorProperty = "Id"
    };

    ICursorPagedResult<UserDto> result = await _mapper.MapCursorPagedAsync(_db.Users, opts);
    return Ok(result);
}
```

---

## License

This project is licensed under the MIT License.
