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
- Opt-out `CursorPaginationOptions.IncludeTotalCount` (default `true`, today's behaviour) skips the
  `COUNT(*)` query cursor paging runs alongside the page query. When set to `false`, only the page
  query runs and `TotalCount` on the result is `-1`; `HasNextPage` / `HasPreviousPage` are unaffected,
  since they were already derived from the page query, not the count.

### Fixed

- Calling `AddMapFluxPagination` more than once no longer loses the earlier call's profiles or
  global options. Each call now adds its `Action<MapperConfigurationBuilder>` and
  `Action<PaginationGlobalOptions>` delegate to a shared accumulator instead of registering a new
  `IMapper` / `PaginationGlobalOptions` singleton that replaced the previous one; the resolved
  `IMapper` applies every accumulated profile delegate, and the resolved `PaginationGlobalOptions`
  applies every accumulated options delegate, in call order. It used to leave only the last call's
  profiles resolvable, so mapping a type registered by an earlier call threw
  `InvalidOperationException: Mapping from … to … is not defined`. A single call's behaviour is
  unchanged.
- `CursorPaginationOptions.Before` now returns the page that precedes the cursor. The query is ordered in
  the reverse of the requested direction, reads `PageSize + 1` rows and the page is turned back into the
  requested order, so with 20 rows and `PageSize = 5` a `Before` pointing at row 16 comes back as
  `11, 12, 13, 14, 15`; it used to come back as `1, 2, 3, 4, 5`, because the cursor only narrowed the query
  and the rows were then read from the start. The flags follow the direction the request moved in:
  `HasPreviousPage` reports whether rows remain behind the page that came back, and `HasNextPage` is `true`
  on a `Before` request. A `Before` request with a separate sort key walks the composite keyset backwards
  the same way. An `After` request is unchanged, the last page included.
- A cursor that cannot be decoded, and a `CursorProperty` the entity does not have, no longer report
  `HasPreviousPage = true` on the first page they fall back to. Both still leave the query unnarrowed by
  default, so the first page is returned as before, but a client is no longer told it can step back from
  it. The new `CursorPaginationOptions.StrictMode` (default `false`) turns both cases into a
  `PaginationStrictModeException` carrying the property name, the cursor and the target type.
- Cursor pagination now honours `SortDescending` and a `SortBy` that differs from `CursorProperty`.
  The cursor filter follows the sort direction, so a descending request continues below the cursor
  instead of above it: the second page of 20 rows with `PageSize = 5` used to come back as `20, 19, 18,
  17` and is now `15, 14, 13, 12, 11`. When `SortBy` names another property, the page is ordered by that
  property with `CursorProperty` as the tie-breaker and the cursor carries both values, so the next page
  resumes at the right row; previously the filter narrowed on `CursorProperty` alone while the rows were
  ordered by `SortBy`, which silently skipped rows. Cursors issued by earlier versions are still
  accepted and, on a request with a separate sort key, are still applied to `CursorProperty` the way
  they were when they were issued. The new public helper `QueryableHelper.BuildCursor` builds the cursor
  for a row and the given options.
- `ToCursorPagedAsync` now throws `ArgumentOutOfRangeException` for `CursorPaginationOptions.PageSize <= 0`
  instead of returning an empty page with `HasNextPage = true`, a result a caller could not act on.
- A `CursorProperty` whose type has no comparison operator is now compared with `CompareTo`, so `string`,
  `Guid` and `enum` cursor properties page instead of throwing. `CursorProperty = "Name"` used to fail with
  `InvalidOperationException: The binary operator GreaterThan is not defined for the types 'System.String'
  and 'System.String'`, and the same happened for an `enum` property and for a `Guid` on a .NET 6 runtime,
  where `Guid` has no comparison operator. `string` is narrowed with `string.Compare`, `Guid` with
  `Guid.CompareTo` on every runtime, and an `enum` is compared as its underlying numeric type; all three
  translate to a plain column comparison in SQL, so the page is still produced by the database. Cursor
  properties of a numeric, `decimal`, `char`, `DateTime`, `DateTimeOffset`, `DateOnly`, `TimeOnly` or
  `TimeSpan` type keep the comparison they had.
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
