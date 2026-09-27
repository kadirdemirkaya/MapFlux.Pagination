# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
