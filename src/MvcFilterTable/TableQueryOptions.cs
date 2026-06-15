namespace MvcFilterTable;

public sealed class TableQueryOptions
{
    public int MaxPageSize { get; init; } = 100;

    public int MaxRegexPatternLength { get; init; } = 200;

    public int MaxRegexCandidateRows { get; init; } = 1_000;

    public TimeSpan RegexTimeout { get; init; } = TimeSpan.FromMilliseconds(100);
}
