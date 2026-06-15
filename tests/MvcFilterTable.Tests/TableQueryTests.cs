using MvcFilterTable;

namespace MvcFilterTable.Tests;

public sealed class TableQueryTests
{
    private static readonly TableDefinition<CookieRow> Cookies = TableDefinition
        .For<CookieRow>()
        .Column("cookie", "Cookie", cookie => cookie.Cookie)
        .Column("price", "Price", cookie => cookie.Price)
        .Column("number", "Quantity", cookie => cookie.Number)
        .Column("instock", "In Stock", cookie => cookie.InStock)
        .Column("secret", "Secret", cookie => cookie.SecretCode, visible: false, filterable: false, sortable: false)
        .Build();

    private static readonly CookieRow[] Rows =
    [
        new("Chocolate Chip", 2.50, 12, true, "A-100"),
        new("Oatmeal Raisin", 1.75, 4, false, "B-200"),
        new("Macaron", 3.25, 30, true, "C-300"),
        new("Sugar Cookie", 1.25, 18, true, "D-400")
    ];

    [Fact]
    public void Apply_sorts_by_allowed_column()
    {
        var request = new TableRequest
        {
            SortBy = "price",
            SortDirection = SortDirection.Descending,
            PageSize = 10
        };

        var page = TableQuery.Apply(Rows.AsQueryable(), Cookies, request);

        Assert.Equal(["Macaron", "Chocolate Chip", "Oatmeal Raisin", "Sugar Cookie"], page.Items.Select(row => row.Cookie));
        Assert.Equal("asc", page.NextSortDirectionFor("price"));
        Assert.Equal("asc", page.NextSortDirectionFor("cookie"));
    }

    [Fact]
    public void Apply_filters_with_equality_comparison_and_bool_values()
    {
        var request = new TableRequest
        {
            Query = "price >= 2.50 and number < 20 and instock = true",
            SortBy = "cookie",
            PageSize = 10
        };

        var page = TableQuery.Apply(Rows.AsQueryable(), Cookies, request);

        Assert.Equal(["Chocolate Chip"], page.Items.Select(row => row.Cookie));
        Assert.Equal(1, page.TotalCount);
    }

    [Fact]
    public void Apply_filters_text_with_a_safe_regex_operator()
    {
        var request = new TableRequest
        {
            Query = "cookie ~ \"^.*(Chip|Raisin)$\"",
            SortBy = "cookie",
            PageSize = 10
        };

        var page = TableQuery.Apply(Rows.AsQueryable(), Cookies, request);

        Assert.Equal(["Chocolate Chip", "Oatmeal Raisin"], page.Items.Select(row => row.Cookie));
    }

    [Fact]
    public void Apply_rejects_unregistered_or_unfilterable_columns()
    {
        var request = new TableRequest
        {
            Query = "secret = A-100"
        };

        var exception = Assert.Throws<TableQueryException>(() => TableQuery.Apply(Rows.AsQueryable(), Cookies, request));

        Assert.Contains("not allowed", exception.Message);
    }

    [Fact]
    public void Apply_rejects_invalid_regex_patterns()
    {
        var request = new TableRequest
        {
            Query = "cookie ~ \"[unterminated\""
        };

        var exception = Assert.Throws<TableQueryException>(() => TableQuery.Apply(Rows.AsQueryable(), Cookies, request));

        Assert.Contains("Invalid regex", exception.Message);
    }

    [Fact]
    public void Apply_paginates_after_filtering()
    {
        var request = new TableRequest
        {
            Query = "instock = true",
            SortBy = "number",
            PageNumber = 2,
            PageSize = 1
        };

        var page = TableQuery.Apply(Rows.AsQueryable(), Cookies, request);

        Assert.Equal(["Sugar Cookie"], page.Items.Select(row => row.Cookie));
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.True(page.HasPreviousPage);
        Assert.True(page.HasNextPage);
    }

    private sealed record CookieRow(string Cookie, double Price, int Number, bool InStock, string SecretCode);
}
