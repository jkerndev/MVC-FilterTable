namespace MvcFilterTable;

public sealed class TableResult<T>
{
    internal TableResult(
        IReadOnlyList<T> items,
        IReadOnlyList<TableColumn<T>> columns,
        int totalCount,
        int pageNumber,
        int pageSize,
        string? query,
        string? sortBy,
        SortDirection sortDirection)
    {
        Items = items;
        VisibleColumns = columns.Where(column => column.IsVisible).ToList();
        TotalCount = totalCount;
        PageNumber = pageNumber;
        PageSize = pageSize;
        Query = query;
        SortBy = sortBy;
        SortDirection = sortDirection;
    }

    public IReadOnlyList<T> Items { get; }

    public IReadOnlyList<TableColumn<T>> VisibleColumns { get; }

    public int TotalCount { get; }

    public int PageNumber { get; }

    public int PageSize { get; }

    public string? Query { get; }

    public string? SortBy { get; }

    public SortDirection SortDirection { get; }

    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPreviousPage => PageNumber > 1;

    public bool HasNextPage => PageNumber < TotalPages;

    public bool IsSortedBy(string columnKey)
    {
        return string.Equals(SortBy, columnKey, StringComparison.OrdinalIgnoreCase);
    }

    public string NextSortDirectionFor(string columnKey)
    {
        if (IsSortedBy(columnKey) && SortDirection == SortDirection.Ascending)
        {
            return "desc";
        }

        return "asc";
    }

    public string SortDirectionText => SortDirection == SortDirection.Descending ? "desc" : "asc";
}
