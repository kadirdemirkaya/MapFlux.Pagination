# MapFlux.Pagination

Advanced Pagination + Object Mapping library for .NET 6.0, 7.0, 8.0, and 9.0, powered by MapFlux and Entity Framework Core.

MapFlux.Pagination allows you to query, filter, sort, and paginate database entities, and automatically map them directly to DTOs in a single database roundtrip.

---

## Features

- ⚡ **Automatic Mapping**: Maps database entities directly to DTOs using MapFlux.
- 🔍 **Dynamic Filtering**: Property-based filtering with operators like Equals, Contains, GreaterThan, etc.
- 🔀 **Multi-Column Sorting**: Order by multiple fields dynamically.
- 🔎 **Global Search**: Case-insensitive search across multiple string properties.
- ⏳ **Cursor-Based Pagination**: High-performance keyset pagination for large datasets.
- 💾 **In-Memory Pagination**: Paginate standard in-memory lists/collections.
- 🛡️ **Input Validation & Safety**: Guardrails against invalid page arguments.

---

## Installation

```bash
dotnet add package MapFlux.Pagination
```

---

## Quick Start

### 1. Define a Mapping Profile

Create a MapFlux mapping profile to define how your entity maps to a DTO:

```csharp
using MapFlux;

public class UserProfile : IMapProfile
{
    public void Configure(IProfileExpression expression)
    {
        expression.CreateMap<User, UserDto>()
            .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.Name));
    }
}
```

### 2. Register in `Program.cs`

```csharp
using MapFlux.Pagination.Extensions;

builder.Services.AddMapFluxPagination(cfg => 
{
    cfg.AddProfile<UserProfile>();
});
```

Optionally configure global limits:

```csharp
builder.Services.AddMapFluxPagination(
    cfg => cfg.AddProfile<UserProfile>(),
    opts => 
    {
        opts.DefaultPageSize = 20;
        opts.MaxPageSize = 100;
    });
```

### 3. Use in Controller

```csharp
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
        var result = await _mapper.MapPagedAsync(_db.Users, opts);
        return Ok(result);
    }
}
```

Response:

```json
{
  "items": [
    { "fullName": "John Doe", "email": "john@example.com" }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 10,
  "totalPages": 15,
  "hasPreviousPage": false,
  "hasNextPage": true,
  "firstItemIndex": 1,
  "lastItemIndex": 10
}
```

---

## Usage Without Mapping

If you don't need DTO mapping, use the `IQueryable` extension directly:

```csharp
using MapFlux.Pagination.Extensions;

var opts = new PaginationOptions { PageNumber = 1, PageSize = 10 };
IPagedResult<User> result = await _db.Users.ToPagedAsync(opts);
```

For in-memory collections:

```csharp
List<User> users = GetCachedUsers();
IPagedResult<User> result = users.ToPaged(opts);
```

---

## Dynamic Filtering & Multi-Sorting

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
    },
    SearchTerm = "john",
    SearchProperties = new[] { "FirstName", "LastName", "Email" }
};

var result = await _mapper.MapPagedAsync(_db.Users, opts);
```

`FilterCriteria.Value` is an `object?`, so the same options can be bound straight from a request body.
JSON numbers, strings and booleans are converted to the target property's type — including a
`Nullable<T>` property, where `null` matches the rows whose value is not set:

```json
{
  "pageNumber": 1,
  "pageSize": 10,
  "filters": [
    { "propertyName": "IsActive", "operator": 0, "value": true },
    { "propertyName": "Age", "operator": 6, "value": 18 }
  ]
}
```

A text value is parsed with the invariant culture first and with the current culture second, so the
same request behaves the same on every server: `"2026-01-01T10:00:01Z"` keeps its UTC instant on a
`DateTime` or `DateTimeOffset` property instead of shifting to the server's local time, `"5.5"`
reaches a `decimal` where the culture uses a decimal comma — and `"5,5"` still reaches it there too —
and `Guid`, `DateOnly` and `TimeOnly` properties accept their usual text form.

---

## Cursor-Based Pagination

For high-performance pagination on large datasets (avoids `OFFSET` performance degradation):

```csharp
[HttpGet("cursor")]
public async Task<IActionResult> GetUsersCursor([FromQuery] string? after, [FromQuery] int size = 20)
{
    var opts = new CursorPaginationOptions 
    { 
        PageSize = size,
        After = after,
        CursorProperty = "Id"
    };

    ICursorPagedResult<UserDto> result = await _mapper.MapCursorPagedAsync(_db.Users, opts);
    return Ok(result);
}
```

Response:

```json
{
  "items": [ ... ],
  "startCursor": "eyJJZCI6MX0=",
  "endCursor": "eyJJZCI6MjB9",
  "hasNextPage": true,
  "hasPreviousPage": false,
  "totalCount": 50000
}
```

---

## License

This project is licensed under the MIT License
