# MVC Filter Table Query Engine Deep Dive

This document explains the query engine in `MvcFilterTable`: what it can do, how requests flow through it, what the query language supports, and where its safety boundaries are.

The engine is intentionally small. It is not a full SQL replacement or LINQ provider. It is a safe, view-oriented query layer for MVC index pages where users need sorting, paging, and basic filters without giving them direct access to your database schema.

## Core Idea

The library takes four inputs:

```csharp
var page = TableQuery.Apply(source, tableDefinition, request, options);
```

- `source`: an `IQueryable<T>` from Entity Framework, LINQ-to-Objects, or another LINQ provider.
- `tableDefinition`: a whitelist of columns the page allows users to sort and filter by.
- `request`: the user's query text, sort column, sort direction, page number, and page size.
- `options`: safety limits for page size and regex processing.

It returns a `TableResult<T>` containing:

- the current page of items
- visible columns for rendering table headers
- total row count after filtering
- paging metadata
- sort metadata for clickable headers

The MVC view is still fully custom. The library does not render HTML. That means one index page can render a plain table, another can hide columns, another can add action buttons, and another can add expandable detail rows.

## Table Definitions

Before a user can query or sort a field, the application must register it:

```csharp
public static readonly TableDefinition<CookieItem> Definition = TableDefinition
    .For<CookieItem>()
    .Column("cookie", "Cookie", cookie => cookie.Cookie)
    .Column("price", "Price", cookie => cookie.Price)
    .Column("number", "Qty", cookie => cookie.Number)
    .Column("instock", "Stock", cookie => cookie.InStock)
    .Column("batch", "Batch", cookie => cookie.BatchCode, visible: false)
    .Build();
```

Each column has:

- `key`: the query-string and query-language name, such as `price`.
- `title`: the display text used by the view, such as `Price`.
- `selector`: the strongly typed property expression, such as `cookie => cookie.Price`.
- `visible`: whether the column appears in `TableResult.VisibleColumns`.
- `filterable`: whether the query language may use this column.
- `sortable`: whether clickable headers or route values may sort by this column.

Column lookup is case-insensitive. A user can write `PRICE >= 2`, `price >= 2`, or `Price >= 2` if the key is registered as `price`.

The column key is not required to match the model property name. This is deliberate. Public query names should be stable and safe even if your internal entity shape changes.

## Visibility vs Query Permissions

Visibility, filtering, and sorting are separate decisions.

A hidden column can still be filterable:

```csharp
.Column("batch", "Batch", cookie => cookie.BatchCode, visible: false)
```

That column will not appear as a table header, but users can still query it:

```text
batch = A-100
```

A visible column can be blocked from filtering or sorting:

```csharp
.Column("notes", "Notes", cookie => cookie.Notes, filterable: false, sortable: false)
```

This allows a page to show data without making that data part of the user-controlled query surface.

## Request Model

The request object is transport-agnostic. MVC query strings are one possible source, but tests or APIs can create it directly:

```csharp
var request = new TableRequest
{
    Query = "price >= 2 and instock = true",
    SortBy = "price",
    SortDirection = SortDirection.Descending,
    PageNumber = 1,
    PageSize = 8
};
```

`PageNumber` is clamped to at least `1`.

`PageSize` is clamped between `1` and `TableQueryOptions.MaxPageSize`.

If `SortBy` is empty, the engine preserves the source ordering.

## Query Language

The query language supports a sequence of conditions joined by `and`.

Examples:

```text
price >= 2
price >= 2 and instock = true
cookie = "Chocolate Chip"
cookie ~ "^.*Chocolate.*$"
number < 10 and instock = false
batch = A-100
```

Supported operators:

| Operator | Meaning | Example |
| --- | --- | --- |
| `=` | equality | `instock = true` |
| `!=` | inequality | `cookie != Macaron` |
| `>` | greater than | `price > 2.50` |
| `>=` | greater than or equal | `number >= 10` |
| `<` | less than | `price < 3` |
| `<=` | less than or equal | `number <= 5` |
| `~` | regex match for text columns | `cookie ~ "^.*Chip$"` |

Only `and` is supported. There is no `or`, grouping, parentheses, negation, functions, nested property traversal logic, or arbitrary expressions.

That limitation is intentional. It keeps the parser readable and keeps the query surface predictable for junior engineers and reviewers.

## Values

Unquoted values run until the next whitespace:

```text
price >= 2.50
instock = true
batch = A-100
```

Quoted values allow spaces:

```text
cookie = "Chocolate Chip"
```

Quoted values support escaping the next character with `\`:

```text
cookie = "Baker \"Special\""
```

Values are converted to the registered column's CLR type using invariant culture. Supported conversion behavior includes:

- strings remain strings
- booleans must be `true` or `false`
- enums are parsed case-insensitively
- numbers use invariant-culture conversion

If a value cannot be converted to the column type, the engine throws `TableQueryException`.

## Type Rules

Equality operators work against the registered column type:

```text
cookie = "Chocolate Chip"
price = 2.50
number = 12
instock = true
```

Comparison operators require comparable non-text, non-boolean values:

```text
price >= 2.50
number < 20
```

These are rejected:

```text
cookie > A
instock < true
```

Regex is text-only:

```text
cookie ~ "^.*Chip$"
```

This is rejected:

```text
price ~ "2.*"
```

## Parser Behavior

The parser is a small hand-written parser in `QueryParser`.

Its steps are:

1. Skip whitespace.
2. Read a column key made of letters, digits, `_`, or `.`.
3. Read one operator.
4. Read one value.
5. Optionally read `and`.
6. Repeat until the end of the query string.

The parser produces internal `QueryCondition` records:

```csharp
internal sealed record QueryCondition(string ColumnKey, QueryOperator Operator, string Value);
```

The parser does not know about database fields. It only knows text syntax. Permission checks happen later against `TableDefinition<T>`.

## Execution Flow

`TableQuery.Apply` follows this order:

1. Validate arguments.
2. Normalize page number and page size.
3. Parse the query string.
4. Resolve each queried column against the whitelist.
5. Convert non-regex conditions into expression trees.
6. Apply database-side filters with `Queryable.Where`.
7. Resolve the requested sort column against the whitelist.
8. Apply sorting.
9. Count filtered rows.
10. Apply paging with `Skip` and `Take`.
11. Return `TableResult<T>`.

For normal comparison and equality filters, the generated expressions stay inside `IQueryable<T>`, so Entity Framework can translate them into SQL.

For regex filters, the flow is different because most databases and LINQ providers cannot safely translate arbitrary .NET regex:

1. Apply all non-regex filters in the database first.
2. Count the remaining candidate rows.
3. Reject the request if candidate rows exceed `MaxRegexCandidateRows`.
4. Move the bounded candidate set into memory.
5. Apply .NET regex with a timeout.
6. Sort and page the in-memory result.

This lets regex exist without giving it unlimited access to large tables.

## Sorting

Sorting is controlled by `SortBy` and `SortDirection`.

```csharp
new TableRequest
{
    SortBy = "price",
    SortDirection = SortDirection.Descending
}
```

The sort column must exist and must be sortable. If not, `TableQueryException` is thrown.

For database-side sorting, the engine uses reflection to call the right generic `Queryable.OrderBy` or `Queryable.OrderByDescending` method based on the column's value type.

For regex queries, sorting happens in memory after regex filtering.

The result object also helps build clickable headers:

```cshtml
<a asp-route-sort="@column.Key"
   asp-route-dir="@Model.Table.NextSortDirectionFor(column.Key)">
    @column.Title
</a>
```

`NextSortDirectionFor` returns:

- `desc` when the current column is already sorted ascending
- `asc` otherwise

This gives the common behavior where clicking the active ascending column reverses it, and clicking a different column starts ascending.

## Paging

Paging is applied after filtering and sorting.

`TableResult<T>` exposes:

- `PageNumber`
- `PageSize`
- `TotalCount`
- `TotalPages`
- `HasPreviousPage`
- `HasNextPage`

`TotalPages` returns `1` even when there are zero rows. That keeps the UI simple because a page can render "Page 1 of 1" instead of handling a zero-page state.

The engine does not currently clamp page numbers down to `TotalPages`. If a user requests a page beyond the end, the result can contain no items while still reporting the real total page count.

## Result Model

`TableResult<T>` is designed for MVC views:

```csharp
public IReadOnlyList<T> Items { get; }
public IReadOnlyList<TableColumn<T>> VisibleColumns { get; }
public int TotalCount { get; }
public int PageNumber { get; }
public int PageSize { get; }
public string? Query { get; }
public string? SortBy { get; }
public SortDirection SortDirection { get; }
```

Useful helper methods:

```csharp
Model.Table.IsSortedBy(column.Key)
Model.Table.NextSortDirectionFor(column.Key)
Model.Table.SortDirectionText
```

The view can use `VisibleColumns` for headers, but it is not required to render every cell dynamically. The sample uses visible columns for headers while manually rendering body cells so it can include formatting, stock pills, batch text, and details buttons.

## MVC Integration Pattern

A controller typically converts query-string values into `TableRequest`:

```csharp
public IActionResult Index(string? q, string? sort, string? dir, int page = 1, int pageSize = 8)
{
    var request = new TableRequest
    {
        Query = q,
        SortBy = sort,
        SortDirection = string.Equals(dir, "desc", StringComparison.OrdinalIgnoreCase)
            ? SortDirection.Descending
            : SortDirection.Ascending,
        PageNumber = page,
        PageSize = pageSize
    };

    var table = TableQuery.Apply(
        _dbContext.Cookies.AsNoTracking(),
        CookieTable.Definition,
        request);

    return View(table);
}
```

The sample catches `TableQueryException` and shows the message in the page instead of returning a 500 error.

## Security Model

The engine is built around allow-listing.

Important protections:

- Users cannot name arbitrary entity properties. They can only use registered column keys.
- Users cannot sort by arbitrary properties. Sort columns must be registered and sortable.
- Users cannot filter hidden internal data unless the application explicitly allows that column to be filterable.
- The engine builds expression trees instead of concatenating SQL strings.
- Values are type-converted, not injected into SQL text.
- Invalid operators, invalid values, unsupported comparisons, and invalid regex patterns throw controlled `TableQueryException` errors.
- Page size is clamped.
- Regex patterns have a maximum length.
- Regex execution has a timeout.
- Regex runs only after a candidate-row limit check.

Security still depends on defining columns carefully. Do not register sensitive fields as filterable if the user should not be able to infer them through counts, sorting, or filtered results.

For example, this is dangerous if regular users should not know anything about `InternalRiskScore`:

```csharp
.Column("risk", "Risk", customer => customer.InternalRiskScore, visible: false)
```

Even if hidden, a user could query:

```text
risk > 80
```

The safe version is:

```csharp
.Column("risk", "Risk", customer => customer.InternalRiskScore, visible: false, filterable: false, sortable: false)
```

## Regex Safety

Regex is useful for power users but can be expensive. This implementation treats regex as a bounded in-memory feature.

Default limits:

```csharp
public sealed class TableQueryOptions
{
    public int MaxPageSize { get; init; } = 100;
    public int MaxRegexPatternLength { get; init; } = 200;
    public int MaxRegexCandidateRows { get; init; } = 1_000;
    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(100);
}
```

The recommended pattern is to combine regex with ordinary filters:

```text
instock = true and cookie ~ "^.*Chocolate.*$"
```

That lets the database reduce the candidate set before regex runs.

If the candidate count is too high, the engine rejects the query and asks the user to add another filter first.

## Error Handling

User-facing query failures throw `TableQueryException`.

Examples:

- unknown column: `secret = A-100`
- unfilterable column: `notes = something`
- invalid operator: `price ~~ 2`
- invalid value: `price = expensive`
- invalid regex: `cookie ~ "[unterminated"`
- regex on a non-string column: `price ~ "2.*"`
- comparison on text or bool: `cookie > A`

MVC controllers should catch `TableQueryException` and render a validation message near the search box.

## What It Does Not Do Yet

The current engine intentionally does not support:

- `or`
- parentheses
- nested boolean logic
- user-defined functions
- date keywords such as `today`
- string operators such as `contains`, `startsWith`, or `endsWith`
- database-native regex translation
- multi-column sorting
- page-number clamping to the last page
- projection to DTOs
- async query execution

These can be added later, but each one should preserve the same design principles: whitelist first, simple parser, typed conversion, provider-friendly expression trees, and bounded expensive operations.

## Extension Points

Good next additions would be:

- `Contains` text operator that translates to `string.Contains`.
- Date/time conversion tests and examples.
- Async `ApplyAsync` for EF Core.
- Multi-column sort definitions controlled by the server.
- A public query parser result for validation previews.
- A reusable tag helper for sort links while keeping table row rendering custom.

Avoid adding features that require raw SQL string generation from user input. If a feature cannot be represented safely as a controlled expression tree or bounded in-memory operation, it should not be part of this query layer.

## Quick Reference

Cookie sample queries:

```text
price >= 2
price >= 2 and instock = true
number < 10
cookie = "Chocolate Chip"
cookie != Macaron
cookie ~ "^.*Chocolate.*$"
batch = A-100
```

Cookie sample route:

```text
/?q=price%20%3E%3D%202%20and%20instock%20%3D%20true&sort=price&dir=desc
```

Minimal application flow:

```csharp
var definition = TableDefinition
    .For<Product>()
    .Column("name", "Name", product => product.Name)
    .Column("price", "Price", product => product.Price)
    .Build();

var request = new TableRequest
{
    Query = q,
    SortBy = sort,
    SortDirection = dir == "desc" ? SortDirection.Descending : SortDirection.Ascending,
    PageNumber = page,
    PageSize = pageSize
};

var result = TableQuery.Apply(db.Products.AsNoTracking(), definition, request);
```
