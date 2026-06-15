using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.RegularExpressions;

namespace MvcFilterTable;

public static class TableQuery
{
    public static TableResult<T> Apply<T>(
        IQueryable<T> source,
        TableDefinition<T> definition,
        TableRequest request,
        TableQueryOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(request);

        options ??= new TableQueryOptions();

        var pageNumber = Math.Max(1, request.PageNumber);
        var pageSize = Math.Clamp(request.PageSize, 1, options.MaxPageSize);
        var conditions = QueryParser.Parse(request.Query);
        var regexConditions = new List<(TableColumn<T> Column, Regex Regex)>();

        source = ApplyDatabaseFilters(source, definition, conditions, options, regexConditions);

        var sortColumn = ResolveSortColumn(definition, request.SortBy);
        var sortBy = sortColumn?.Key;

        IReadOnlyList<T> items;
        int totalCount;

        if (regexConditions.Count > 0)
        {
            var candidateCount = source.Count();
            if (candidateCount > options.MaxRegexCandidateRows)
            {
                throw new TableQueryException($"Regex queries are limited to {options.MaxRegexCandidateRows} candidate rows. Add another filter first.");
            }

            var rows = ApplyRegexFilters(source.AsEnumerable(), regexConditions);
            rows = ApplyEnumerableSort(rows, sortColumn, request.SortDirection);

            totalCount = rows.Count();
            items = rows.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        }
        else
        {
            source = ApplyQueryableSort(source, sortColumn, request.SortDirection);

            totalCount = source.Count();
            items = source.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();
        }

        return new TableResult<T>(
            items,
            definition.Columns,
            totalCount,
            pageNumber,
            pageSize,
            request.Query,
            sortBy,
            request.SortDirection);
    }

    private static IQueryable<T> ApplyDatabaseFilters<T>(
        IQueryable<T> source,
        TableDefinition<T> definition,
        IReadOnlyList<QueryCondition> conditions,
        TableQueryOptions options,
        List<(TableColumn<T> Column, Regex Regex)> regexConditions)
    {
        var parameter = Expression.Parameter(typeof(T), "row");
        Expression? filter = null;

        foreach (var condition in conditions)
        {
            var column = ResolveFilterColumn(definition, condition.ColumnKey);

            if (condition.Operator == QueryOperator.Regex)
            {
                regexConditions.Add((column, CreateRegex(column, condition.Value, options)));
                continue;
            }

            var conditionExpression = BuildConditionExpression(parameter, column, condition);
            filter = filter == null ? conditionExpression : Expression.AndAlso(filter, conditionExpression);
        }

        if (filter == null)
        {
            return source;
        }

        return source.Where(Expression.Lambda<Func<T, bool>>(filter, parameter));
    }

    private static TableColumn<T> ResolveFilterColumn<T>(TableDefinition<T> definition, string columnKey)
    {
        if (!definition.TryGetColumn(columnKey, out var column) || column is not { IsFilterable: true })
        {
            throw new TableQueryException($"Column '{columnKey}' is not allowed in queries.");
        }

        return column;
    }

    private static TableColumn<T>? ResolveSortColumn<T>(TableDefinition<T> definition, string? columnKey)
    {
        if (string.IsNullOrWhiteSpace(columnKey))
        {
            return null;
        }

        if (!definition.TryGetColumn(columnKey, out var column) || column is not { IsSortable: true })
        {
            throw new TableQueryException($"Column '{columnKey}' is not allowed for sorting.");
        }

        return column;
    }

    private static Expression BuildConditionExpression<T>(ParameterExpression parameter, TableColumn<T> column, QueryCondition condition)
    {
        var left = column.BodyFor(parameter);
        var value = ConvertValue(condition.Value, column.ValueType, column.Key);
        var right = Expression.Constant(value, column.ValueType);

        return condition.Operator switch
        {
            QueryOperator.Equals => Expression.Equal(left, right),
            QueryOperator.NotEquals => Expression.NotEqual(left, right),
            QueryOperator.GreaterThan => BuildComparison(left, right, condition),
            QueryOperator.GreaterThanOrEqual => BuildComparison(left, right, condition),
            QueryOperator.LessThan => BuildComparison(left, right, condition),
            QueryOperator.LessThanOrEqual => BuildComparison(left, right, condition),
            _ => throw new TableQueryException($"Operator '{condition.Operator}' is not supported for column '{column.Key}'.")
        };
    }

    private static Expression BuildComparison(Expression left, Expression right, QueryCondition condition)
    {
        var type = Nullable.GetUnderlyingType(left.Type) ?? left.Type;

        if (type == typeof(string) || type == typeof(bool) || !typeof(IComparable).IsAssignableFrom(type))
        {
            throw new TableQueryException($"Column '{condition.ColumnKey}' does not support comparison operators.");
        }

        return condition.Operator switch
        {
            QueryOperator.GreaterThan => Expression.GreaterThan(left, right),
            QueryOperator.GreaterThanOrEqual => Expression.GreaterThanOrEqual(left, right),
            QueryOperator.LessThan => Expression.LessThan(left, right),
            QueryOperator.LessThanOrEqual => Expression.LessThanOrEqual(left, right),
            _ => throw new TableQueryException($"Operator '{condition.Operator}' is not a comparison operator.")
        };
    }

    private static object? ConvertValue(string value, Type targetType, string columnKey)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        var type = nullableType ?? targetType;

        try
        {
            if (type == typeof(string))
            {
                return value;
            }

            if (type == typeof(bool))
            {
                if (bool.TryParse(value, out var boolValue))
                {
                    return boolValue;
                }

                throw new FormatException("Boolean values must be true or false.");
            }

            if (type.IsEnum)
            {
                return Enum.Parse(type, value, ignoreCase: true);
            }

            return Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception exception) when (exception is FormatException or InvalidCastException or OverflowException or ArgumentException)
        {
            throw new TableQueryException($"Value '{value}' is not valid for column '{columnKey}'.");
        }
    }

    private static Regex CreateRegex<T>(TableColumn<T> column, string pattern, TableQueryOptions options)
    {
        if (column.ValueType != typeof(string))
        {
            throw new TableQueryException($"Column '{column.Key}' must be text to use the regex operator.");
        }

        if (pattern.Length > options.MaxRegexPatternLength)
        {
            throw new TableQueryException($"Regex patterns are limited to {options.MaxRegexPatternLength} characters.");
        }

        try
        {
            return new Regex(pattern, RegexOptions.CultureInvariant, options.RegexTimeout);
        }
        catch (ArgumentException exception)
        {
            throw new TableQueryException($"Invalid regex for column '{column.Key}': {exception.Message}");
        }
    }

    private static IEnumerable<T> ApplyRegexFilters<T>(
        IEnumerable<T> rows,
        IReadOnlyList<(TableColumn<T> Column, Regex Regex)> regexConditions)
    {
        foreach (var condition in regexConditions)
        {
            rows = rows.Where(row =>
            {
                var value = condition.Column.GetValue(row) as string ?? string.Empty;
                return condition.Regex.IsMatch(value);
            });
        }

        return rows;
    }

    private static IQueryable<T> ApplyQueryableSort<T>(
        IQueryable<T> source,
        TableColumn<T>? column,
        SortDirection direction)
    {
        if (column == null)
        {
            return source;
        }

        var methodName = direction == SortDirection.Descending ? nameof(Queryable.OrderByDescending) : nameof(Queryable.OrderBy);
        var method = typeof(Queryable).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method => method.Name == methodName && method.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(T), column.ValueType);

        return (IQueryable<T>)method.Invoke(null, [source, column.Selector])!;
    }

    private static IEnumerable<T> ApplyEnumerableSort<T>(
        IEnumerable<T> rows,
        TableColumn<T>? column,
        SortDirection direction)
    {
        if (column == null)
        {
            return rows;
        }

        return direction == SortDirection.Descending
            ? rows.OrderByDescending(column.GetValue)
            : rows.OrderBy(column.GetValue);
    }
}
