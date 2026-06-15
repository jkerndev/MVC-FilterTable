namespace MvcFilterTable;

internal sealed record QueryCondition(string ColumnKey, QueryOperator Operator, string Value);
