using System.Linq.Expressions;

namespace MvcFilterTable;

public static class TableDefinition
{
    public static TableDefinitionBuilder<T> For<T>()
    {
        return new TableDefinitionBuilder<T>();
    }
}

public sealed class TableDefinition<T>
{
    private readonly Dictionary<string, TableColumn<T>> _columns;

    internal TableDefinition(IEnumerable<TableColumn<T>> columns)
    {
        _columns = columns.ToDictionary(column => column.Key, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TableColumn<T>> Columns => _columns.Values.ToList();

    public IReadOnlyList<TableColumn<T>> VisibleColumns => _columns.Values.Where(column => column.IsVisible).ToList();

    public bool TryGetColumn(string? key, out TableColumn<T>? column)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            column = null;
            return false;
        }

        return _columns.TryGetValue(key, out column);
    }
}

public sealed class TableDefinitionBuilder<T>
{
    private readonly List<TableColumn<T>> _columns = [];

    public TableDefinitionBuilder<T> Column<TValue>(
        string key,
        string title,
        Expression<Func<T, TValue>> selector,
        bool visible = true,
        bool filterable = true,
        bool sortable = true)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Column key is required.", nameof(key));
        }

        if (_columns.Any(column => string.Equals(column.Key, key, StringComparison.OrdinalIgnoreCase)))
        {
            throw new ArgumentException($"Column '{key}' is already registered.", nameof(key));
        }

        _columns.Add(new TableColumn<T>(key, title, selector, typeof(TValue), visible, filterable, sortable));
        return this;
    }

    public TableDefinition<T> Build()
    {
        return new TableDefinition<T>(_columns);
    }
}
