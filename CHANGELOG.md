# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

- Opt-in deterministic page order for offset pagination: `PaginationOptions.DefaultSortProperty` orders
  ascending by the named property, and `PaginationOptions.EnsureDeterministicOrder` falls back to the
  entity's key (`[Key]`, otherwise `Id` or `<TypeName>Id`) when no property is named or the named one
  does not exist. Both apply only when the request produced no order of its own — `SortBy` and
  `SortCriterias` still win — and both can be configured globally through
  `PaginationGlobalOptions.DefaultSortProperty` / `EnsureDeterministicOrder`, where a per-request value
  wins over the configured one. A request that names no order used to translate to `LIMIT`/`OFFSET` with
  no `ORDER BY`, which lets a relational database return the same row on two pages or on none. Both
  settings default to off, so the generated SQL of an existing request is unchanged. With `StrictMode`
  on, a `DefaultSortProperty` the type does not have, or a type whose key cannot be resolved, throws
  `PaginationStrictModeException` instead of silently staying unordered. The new public helper
  `QueryableHelper.ApplyDefaultOrdering` applies the same stage on its own.
- Opt-in overloads `IEnumerable<T>.ToPaged(opts, applyPipeline: true)` and the mapped
  `ToPaged<TSource, TDest>(mapper, opts, applyPipeline: true)` run the same filter/search/sort pipeline
  used by the `IQueryable` extensions, in memory (`AsQueryable()` + `QueryableHelper.ApplyFullPipeline`).
  `ToPaged(opts)` without the new parameter keeps slicing the collection by page only, ignoring
  `Filters`, `SearchTerm` and sorting exactly as before.

### Fixed

- Cursors are now written losslessly and independently of the current culture. `QueryableHelper.EncodeCursor`
  (signature unchanged) writes a versioned payload inside the same Base64 envelope: `DateTime` and
  `DateTimeOffset` round-trip through `"O"` and keep their sub-second precision, `double` and `float` through
  `"R"`, and every other value through `CultureInfo.InvariantCulture`. A `CreatedAt` cursor used to lose
  everything below the second, so the next page repeated the rows of the previous one; a cursor issued under
  `tr-TR` and decoded under another culture used to resolve to a different value (a `decimal` `5,5` read back
  as `55`), which returned the first page again or an empty one. Cursors issued by earlier versions keep
  decoding exactly as they did — `QueryableHelper.DecodeCursor` reads the versioned payload when it is
  present and falls back to the previous format otherwise, so clients holding an old cursor keep paging.
- `PagedResult<T>.TotalPages` no longer overflows to `int.MaxValue` (with `HasNextPage` then `true`)
  when `PageSize` is `0` or negative; it now returns `0`, and `HasNextPage` follows as `false`. The
  constructor still accepts any `PageSize` without throwing.
- `ToPagedAsync` and `ToCursorPagedAsync` now fall back to a synchronous `Count`/`ToList` when the
  source `IQueryable`'s provider does not implement `IAsyncQueryProvider` (e.g. a plain
  `list.AsQueryable()`), instead of throwing `InvalidOperationException`. A source backed by an EF
  Core async provider is unaffected and still runs the async path.
- `PaginationOptions.Skip` now computes the offset as a `long` internally before narrowing back to
  `int`; a page number large enough to overflow `int` arithmetic (e.g. `PageNumber = int.MaxValue`
  with `PageSize = 100`) used to wrap into a negative offset and silently return page one instead of
  the requested page. It now clamps to `int.MaxValue`, which yields an empty page while `TotalCount`
  stays correct. The public `Skip` type is unchanged (`int`).
- `PaginationGlobalOptions.MaxPageSize` is now enforced: `IPaginatedMapper<,>.MapPagedAsync` and
  `MapCursorPagedAsync` clamp the requested `PageSize` down to the configured limit before running
  the query, instead of the registered options being ignored. A caller that has configured a limit
  and requests more than it now gets the limit back instead of the full requested page size. Callers
  that never registered `PaginationGlobalOptions`, and the DI-independent `ToPagedAsync` /
  `ToCursorPagedAsync` extension methods, are unaffected.

### Added

- Opt-in `PaginationOptions.StrictMode`: when enabled, an unknown filter/search/sort property, a
  filter value that cannot be converted to the target property's type, or an operator that does not
  apply to that type (such as `Contains` on a non-string property) now throws a
  `PaginationStrictModeException` carrying the property name, the value and the target type, instead
  of being silently dropped. Default is `false` — existing callers keep today's silent behaviour.

### Fixed

- `FilterCriteria.Value` arriving as JSON (a `JsonElement`, as when `PaginationOptions` is bound from
  a request body) is now converted to the filtered property's type instead of being discarded, which
  silently dropped the filter and returned every row. Numbers, strings and booleans are converted,
  `Nullable<T>` properties use their underlying type, and a JSON `null` on a property that accepts
  null matches the rows whose value is not set.
- `SortCriterias` no longer throws `InvalidOperationException` when the first entry names an unknown
  property and `StrictMode` is off: the first entry that names a known property now becomes the
  `OrderBy`, instead of every entry after the dropped one being wired as a `ThenBy` with no preceding
  `OrderBy`.
- A filter targeting an `enum` property is no longer silently dropped: the value now converts from an
  enum member name (case-insensitive) or its underlying integer, both matching a value already
  covered by the property's type. A name or integer with no matching enum member still drops the
  filter, same as any other unrecognized filter value.
- A text filter value is no longer interpreted with the server's culture and time zone. Strings are
  now parsed with the invariant culture first, so a `DateTime` or `DateTimeOffset` value such as
  `"2026-01-01T10:00:01Z"` keeps its UTC instant instead of shifting to local time, and `"5.5"`
  reaches a `decimal`, `double` or `float` property under a culture that uses a decimal comma. When
  the invariant attempt fails the current culture is tried, so values that worked before — `"5,5"` on
  a server with a decimal comma — keep working. `Guid`, `DateOnly` and `TimeOnly` properties are
  parsed explicitly instead of dropping the filter and returning every row.
- A filter value of `null` against `Equals` or `NotEquals` on a non-nullable property no longer throws
  `ArgumentException`. `Equals` now matches no rows and `NotEquals` matches every row; with
  `PaginationOptions.StrictMode` enabled it throws `PaginationStrictModeException` instead. Behaviour
  on a nullable property is unchanged.
- `SearchTerm` is now lowercased with the invariant culture instead of the server's current culture.
  Under `tr-TR`, `"IDEM"` used to lowercase to `"ıdem"` (dotless i), which never matched the database
  side's culture-independent `lower()`, silently returning no rows. The property values themselves
  are still lowercased in the query expression, unaffected by this change.

### Security

- Patched a High severity transitive dependency vulnerability (net6.0, net8.0, net9.0) and updated
  the `MapFlux` dependency to its latest patch release.

## [1.1.0] - 2026-05-21

### Added

- Dynamic filtering over `IQueryable` with `FilterCriteria` (`Equals`, `NotEquals`, `Contains`,
  `StartsWith`, `EndsWith`, `GreaterThan[OrEqual]`, `LessThan[OrEqual]`).
- Multi-property sorting through `SortCriteria` / `SortCriterias`, taking precedence over `SortBy`.
- Case-insensitive global search across named or all public `string` properties via `SearchTerm` /
  `SearchProperties`.
- Cursor-based pagination: `CursorPaginationOptions`, `CursorPagedResult<T>`,
  `MapCursorPagedAsync`, `ToCursorPagedAsync`.
- `PaginationGlobalOptions` for global pagination configuration.
- Input validation on `PaginationOptions` (`PageNumber` / `PageSize` below 1 throw
  `ArgumentOutOfRangeException`).

## [1.0.0] - Initial release

### Added

- Offset pagination over `IQueryable` and `IEnumerable`, mapped to DTOs through MapFlux
  (`IPaginatedMapper`, `MapPagedAsync`, `MapPaged`, `ToPagedAsync`, `ToPaged`).
- Dependency injection registration via `AddMapFluxPagination`.

[Unreleased]: https://github.com/kadirdemirkaya/PaginationFlux/compare/v1.1.0...HEAD
[1.1.0]: https://github.com/kadirdemirkaya/PaginationFlux/releases/tag/v1.1.0
