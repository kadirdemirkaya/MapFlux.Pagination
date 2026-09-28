namespace MapFlux.Pagination.Models;

/// <summary>
/// Options applied to every request handled by the <see cref="MapFlux.IMapper"/>/<c>IPaginatedMapper&lt;,&gt;</c>
/// registered through <c>AddMapFluxPagination</c>.
/// </summary>
public class PaginationGlobalOptions
{
    /// <summary>The default page size when a request does not specify one.</summary>
    public int DefaultPageSize { get; set; } = 10;

    /// <summary>The upper bound every request's page size is clamped to.</summary>
    public int MaxPageSize { get; set; } = 100;

    /// <summary>The default page number when a request does not specify one.</summary>
    public int DefaultPageNumber { get; set; } = 1;

    /// <summary>The default <see cref="PaginationOptions.StrictMode"/> applied when a request does not set it.</summary>
    public bool StrictMode { get; set; }

    /// <summary>The default <see cref="PaginationOptions.DefaultSortProperty"/> applied when a request does not set it.</summary>
    public string? DefaultSortProperty { get; set; }

    /// <summary>The default <see cref="PaginationOptions.EnsureDeterministicOrder"/> applied when a request does not set it.</summary>
    public bool EnsureDeterministicOrder { get; set; }

    /// <summary>When <see langword="true"/>, the registered mapper's configuration is validated the first time it is resolved.</summary>
    public bool ValidateOnStart { get; set; }
}
