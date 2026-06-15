namespace MvcFilterTable;

public sealed class TableRequest
{
    public string? Query { get; init; }

    public string? SortBy { get; init; }

    public SortDirection SortDirection { get; init; } = SortDirection.Ascending;

    public int PageNumber { get; init; } = 1;

    public int PageSize { get; init; } = 25;
}
