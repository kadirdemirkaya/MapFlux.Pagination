# MapFlux.Pagination

Pagination + object mapping for .NET, powered by [MapFlux](https://github.com/kadirdemirkaya/MapFlux).

## Installation

```bash
dotnet add package MapFlux.Pagination
```

## Features

- ✅ **Seamless Mapping**: Automatically maps source entities to destination DTOs during pagination.
- ✅ **Dynamic Sorting**: Support for `SortBy` and `SortDescending` directly in options.
- ✅ **IQueryable Integration**: Fluent `ToPagedAsync` extension methods for EF Core.
- ✅ **Mapper Extensions**: `MapList<S, D>` convenience method for collections.
- ✅ **Metadata Included**: Returns total count, page number, total pages, and navigation flags.
- ✅ **DI Ready**: Single-line registration for the entire mapping + pagination stack.

## Quick Start

### 1. Register in Program.cs

```csharp
builder.Services.AddMapFluxPagination(cfg => 
{
    cfg.AddProfile<UserProfile>();
    cfg.AddProfile<ProductProfile>();
});
```

### 2. Inject and Use in Controllers

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
    public async Task<IActionResult> GetUsers([FromQuery] int page = 1)
    {
        var opts = new PaginationOptions { PageNumber = page, PageSize = 20 };
        var result = await _mapper.MapPagedAsync(_db.Users, opts);
        return Ok(result);
    }
}
```

## License

MIT
