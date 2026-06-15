using System.Linq.Expressions;

namespace MvcFilterTable;

public sealed class TableColumn<T>
{
    internal TableColumn(
        string key,
        string title,
        LambdaExpression selector,
        Type valueType,
        bool visible,
        bool filterable,
        bool sortable)
    {
        Key = key;
        Title = title;
        Selector = selector;
        ValueType = valueType;
        IsVisible = visible;
        IsFilterable = filterable;
        IsSortable = sortable;
    }

    public string Key { get; }

    public string Title { get; }

    public Type ValueType { get; }

    public bool IsVisible { get; }

    public bool IsFilterable { get; }

    public bool IsSortable { get; }

    internal LambdaExpression Selector { get; }

    internal Expression BodyFor(ParameterExpression parameter)
    {
        var originalParameter = Selector.Parameters.Single();
        return new ReplaceParameterVisitor(originalParameter, parameter).Visit(Selector.Body)
            ?? throw new InvalidOperationException("Could not build a column expression.");
    }

    internal object? GetValue(T item)
    {
        return Selector.Compile().DynamicInvoke(item);
    }
}
