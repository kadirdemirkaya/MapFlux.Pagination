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

Optionally configure global limits. `MaxPageSize` is enforced by `IPaginatedMapper<,>`: a
`PaginationOptions.PageSize` above the configured limit is clamped down to it before the query runs.

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

By default, `ToPaged` only slices the collection by page — filters, search and sorting on `opts` are
ignored. Pass `applyPipeline: true` to run the same filter/search/sort pipeline used by the
`IQueryable` extensions, in memory:

```csharp
IPagedResult<User> result = users.ToPaged(opts, applyPipeline: true);
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

By default, an unknown property name, a value that cannot be converted, or an operator that does not
apply to the target type is silently ignored — the filter, search term or sort is dropped and the rest
of the request still runs. Set `StrictMode` to opt into an explanatory `PaginationStrictModeException`
instead, so a typo in a property name or a bad value fails fast rather than quietly returning
unfiltered rows:

```csharp
var opts = new PaginationOptions
{
    StrictMode = true,
    Filters = new List<FilterCriteria>
    {
        new() { PropertyName = "Age", Operator = FilterOperator.Equals, Value = "abc" }
    }
};

// throws PaginationStrictModeException: property name, value and target type are all on the exception
var result = await _mapper.MapPagedAsync(_db.Users, opts);
```

`StrictMode` covers `Filters`, `SearchProperties` and sorting (`SortBy` / `SortCriterias`) alike. It is
recommended for new code; existing callers keep today's silent behaviour until they opt in.

### Deterministic Page Order

A page request that names no sort order translates to `LIMIT`/`OFFSET` with no `ORDER BY`, and a
relational database is then free to return rows in any order — the same row can appear on two pages, or
on none. Two opt-in settings add a fallback order that only applies when the request itself did not
produce one (`SortBy` and `SortCriterias` always win):

```csharp
var opts = new PaginationOptions
{
    PageNumber = 1,
    PageSize = 20,
    DefaultSortProperty = "CreatedAt",
    EnsureDeterministicOrder = true
};
```

- `DefaultSortProperty` orders ascending by the named property.
- `EnsureDeterministicOrder` falls back to the entity's key — the property marked with `[Key]`,
  otherwise `Id` or `<TypeName>Id`, matched case-insensitively — when `DefaultSortProperty` is unset or
  names a property the type does not have.

Both can also be configured once for every `IPaginatedMapper<,>` call, and a per-request value wins over
the configured one:

```csharp
builder.Services.AddMapFluxPagination(
    cfg => cfg.AddProfile<UserProfile>(),
    opts =>
    {
        opts.DefaultSortProperty = "CreatedAt";
        opts.EnsureDeterministicOrder = true;
    });
```

Both default to off, so the generated SQL of an existing request is unchanged until you opt in. When a
property cannot be resolved and no key is found either, the query stays unordered — unless `StrictMode`
is on, which turns it into a `PaginationStrictModeException`.

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

### Cursor format

A cursor is an opaque Base64 string — treat it as a token to hand back unchanged, not as a value to
parse. Its payload is written in a round-trippable, culture-independent form, so a cursor is read back
as the exact value it was issued for: a `DateTime` / `DateTimeOffset` keeps its sub-second precision,
and a `decimal` or `double` keeps its full precision and means the same number regardless of the
culture the server ran under when the cursor was issued.

Cursors issued by earlier versions of the package are still accepted: the decoder recognises the older
payload and reads it the way it was written, so clients holding an old cursor keep paging without a
reset. Those older cursors remain as precise as they were — a `DateTime` cursor written in the old
format still carries only whole seconds.

---

## License

This project is licensed under the MIT License
