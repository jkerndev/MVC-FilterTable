namespace MvcFilterTable;

public sealed class TableQueryException : Exception
{
    public TableQueryException(string message)
        : base(message)
    {
    }
}
