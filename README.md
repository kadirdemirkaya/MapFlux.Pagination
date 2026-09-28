# MapFlux.Pagination

<p align="center"><img src="assets/icon.png" alt="MapFlux.Pagination logo" width="112" /></p>

| Package | Downloads | License |
|---------|-----------|---------|
| [![NuGet](https://img.shields.io/nuget/v/MapFlux.Pagination)](https://www.nuget.org/packages/MapFlux.Pagination) | [![Downloads](https://img.shields.io/nuget/dt/MapFlux.Pagination)](https://www.nuget.org/packages/MapFlux.Pagination) | [![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/kadirdemirkaya/MapFlux.Pagination/blob/main/LICENSE) |

Filter, sort, search and paginate an `IQueryable`, with the page mapped straight to DTOs through MapFlux.

## Installation

```bash
dotnet add package MapFlux.Pagination
```

## Quick Start

```csharp
using MapFlux;
using MapFlux.Pagination.Core;
using MapFlux.Pagination.Extensions;
using MapFlux.Pagination.Models;

public class UserProfile : Profile
{
    public override void Configure(IMapperConfigurationExpression cfg)
    {
        cfg.CreateMap<User, UserDto>(opt => opt.ForMember(d => d.FullName, m => m.MapFrom(s => s.Name)));
    }
}

builder.Services.AddMapFluxPagination(cfg => cfg.AddProfile<UserProfile>());

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
    { "fullName": "John Doe" }
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

`MapPagedAsync` runs two queries against `_db.Users` — a `COUNT(*)` for `totalCount` and the page
itself — then maps the page in memory; it is not a single roundtrip. Skip the count on cursor paging
with `IncludeTotalCount = false` (see below) when you don't need it.

---

## Features

- ⚡ **Automatic Mapping**: Maps database entities directly to DTOs using MapFlux.
- 🔍 **Dynamic Filtering**: Property-based filtering with operators like Equals, Contains, GreaterThan, etc.
- 🔀 **Multi-Column Sorting**: Order by multiple fields dynamically.
- 🔎 **Global Search**: Case-insensitive search across multiple string properties.
- ⏳ **Cursor-Based Pagination**: High-performance keyset pagination for large datasets.
- 💾 **In-Memory Pagination**: Paginate standard in-memory lists/collections, with an opt-in filter/search/sort pipeline.
- 🛡️ **Input Validation & Safety**: Guardrails against invalid page arguments.

---

## Global Options

Configure default and maximum page sizes once for every `IPaginatedMapper<,>` call. `MaxPageSize` is
enforced by `IPaginatedMapper<,>`: a `PaginationOptions.PageSize` above the configured limit is clamped
down to it before the query runs.

```csharp
builder.Services.AddMapFluxPagination(
    cfg => cfg.AddProfile<UserProfile>(),
    opts =>
    {
        opts.DefaultPageSize = 20;
        opts.MaxPageSize = 100;
    });
```

Opt in to configuration validation with `ValidateOnStart`. When set, an incomplete map throws
`InvalidOperationException` as soon as `IMapper` is resolved from the container instead of on the
first `MapPagedAsync` call. Resolve it once right after building the app to fail fast at startup:

```csharp
builder.Services.AddMapFluxPagination(
    cfg => cfg.AddProfile<UserProfile>(),
    opts => opts.ValidateOnStart = true);

var app = builder.Build();
app.Services.GetRequiredService<IMapper>();
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

`Contains`, `StartsWith` and `EndsWith` translate to the provider's own string comparison, so their
case-sensitivity follows the column's collation — case-sensitive on SQLite's default collation, for
example, even though `SearchTerm` matching is always lowercased and therefore case-insensitive
everywhere.

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

### Skipping the total count

Every cursor page runs a `COUNT(*)` alongside the page query by default (`IncludeTotalCount = true`),
matching `totalCount` above. On a large table that count is often the more expensive of the two
queries and cursor paging does not need it to know whether there is a next page. Set
`IncludeTotalCount = false` to skip it — only the page query runs, and `TotalCount` on the result is
`-1`:

```csharp
var opts = new CursorPaginationOptions
{
    PageSize = size,
    After = after,
    CursorProperty = "Id",
    IncludeTotalCount = false
};

ICursorPagedResult<UserDto> result = await _mapper.MapCursorPagedAsync(_db.Users, opts);
// result.TotalCount == -1, result.HasNextPage / HasPreviousPage are unaffected
```

### Sort direction and a separate sort key

`SortDescending` controls both the page order and the direction the cursor moves in: with
`SortDescending = true` the rows come back in descending order and `After` continues *below* the cursor,
so walking the pages of 20 rows with `PageSize = 5` yields `20…16`, then `15…11`.

`SortBy` may name a property other than `CursorProperty`. The page is then ordered by `SortBy` with
`CursorProperty` as the tie-breaker, and the cursor carries both values, so the next page resumes at
exactly the right row even when many rows share the same sort key:

```csharp
var opts = new CursorPaginationOptions
{
    PageSize = 20,
    SortBy = "CreatedAt",
    CursorProperty = "Id",
    SortDescending = true
};
```

Both properties are read case-insensitively. Leaving `SortBy` unset orders by `CursorProperty` alone.

### Paging backwards with `Before`

`Before` returns the page that ends just short of the cursor. The query is ordered in the reverse of the
requested direction, so the rows nearest the cursor are the ones read, and the page is turned back into
the requested order before it is returned. With 20 rows and `PageSize = 5`, `Before = <cursor of row 16>`
yields `11, 12, 13, 14, 15` — the page a client lands on when it steps back from the page that starts at
16. The flags follow the direction the request moved in: `HasPreviousPage` reports whether further rows
exist behind the page that came back, and `HasNextPage` is `true`, because the cursor was issued for a
row further on. `After` and `Before` are alternatives; when both are set, `After` wins.

```csharp
var previous = new CursorPaginationOptions
{
    PageSize = 20,
    CursorProperty = "Id",
    Before = page.StartCursor
};
```

### Cursors a request cannot use

A cursor that cannot be decoded — a truncated or hand-edited token, or one issued for a property of a
different type — and a `CursorProperty` the entity does not have both leave the query unnarrowed, so the
first page comes back. `HasPreviousPage` is `false` in that case, so a client is not invited to step back
to a page that is not there. Set `StrictMode` on `CursorPaginationOptions` to get a
`PaginationStrictModeException` instead, carrying the property name, the cursor and the target type:

```csharp
var opts = new CursorPaginationOptions
{
    PageSize = 20,
    CursorProperty = "Id",
    After = after,
    StrictMode = true
};
```

### Cursor format

A cursor is an opaque Base64 string — treat it as a token to hand back unchanged, not as a value to
parse. Its payload is written in a round-trippable, culture-independent form, so a cursor is read back
as the exact value it was issued for: a `DateTime` / `DateTimeOffset` keeps its sub-second precision,
and a `decimal` or `double` keeps its full precision and means the same number regardless of the
culture the server ran under when the cursor was issued.

A cursor issued for a request whose `SortBy` differs from `CursorProperty` carries the two values
together; one issued for a single sort key carries just that value. Both are Base64 and both are read
back by the same decoder, so a client never has to tell them apart.

Cursors issued by earlier versions of the package are still accepted: the decoder recognises the older
payload and reads it the way it was written, so clients holding an old cursor keep paging without a
reset. Those older cursors remain as precise as they were — a `DateTime` cursor written in the old
format still carries only whole seconds — and an old cursor presented on a request that now uses a
separate sort key is still applied to `CursorProperty`, as it was when the cursor was issued.

### Cursor property types

`CursorProperty` can name any property whose values can be ordered: the integral and floating-point
numeric types, `decimal`, `char`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly`, `TimeSpan`,
`string`, `Guid` and an `enum`. For `string` the page is narrowed with `string.Compare`, for `Guid` with
`Guid.CompareTo`, and an `enum` is compared as its underlying numeric type — all three translate to a
plain column comparison in SQL, so paging stays server-side.

Two things follow from letting the database do the comparison:

- **`string`**: the page boundary follows the column's collation, exactly as the `ORDER BY` of the same
  query does. A case- or accent-insensitive collation therefore orders — and pages — the rows the way
  that collation dictates, which need not match .NET's own string ordering.
- **`Guid`**: databases do not agree on how to order a `Guid`. SQL Server compares the last six bytes
  first, PostgreSQL (`uuid`) and providers that store the value as text compare it byte by byte or
  character by character, and .NET's own `Guid` ordering is a third one. The pages themselves stay
  correct and non-overlapping, because the filter and the `ORDER BY` run under the same rules; only the
  sequence the rows come back in differs per database. Where a stable, portable order matters, page on a
  sequential key and keep the `Guid` as the payload.

---

## License

This project is licensed under the [MIT License](https://github.com/kadirdemirkaya/MapFlux.Pagination/blob/main/LICENSE).
