namespace MvcFilterTable;

internal static class QueryParser
{
    public static IReadOnlyList<QueryCondition> Parse(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var parser = new Parser(query);
        return parser.Parse();
    }

    private sealed class Parser
    {
        private readonly string _query;
        private int _index;

        public Parser(string query)
        {
            _query = query;
        }

        public IReadOnlyList<QueryCondition> Parse()
        {
            var conditions = new List<QueryCondition>();

            while (true)
            {
                SkipWhiteSpace();

                if (IsAtEnd)
                {
                    return conditions;
                }

                var columnKey = ReadColumnKey();
                SkipWhiteSpace();
                var queryOperator = ReadOperator();
                SkipWhiteSpace();
                var value = ReadValue();

                conditions.Add(new QueryCondition(columnKey, queryOperator, value));

                SkipWhiteSpace();
                TryReadAnd();
            }
        }

        private bool IsAtEnd => _index >= _query.Length;

        private string ReadColumnKey()
        {
            var start = _index;

            while (!IsAtEnd && (char.IsLetterOrDigit(_query[_index]) || _query[_index] == '_' || _query[_index] == '.'))
            {
                _index++;
            }

            if (start == _index)
            {
                throw new TableQueryException("Expected a column name.");
            }

            return _query[start.._index];
        }

        private QueryOperator ReadOperator()
        {
            if (TryRead(">="))
            {
                return QueryOperator.GreaterThanOrEqual;
            }

            if (TryRead("<="))
            {
                return QueryOperator.LessThanOrEqual;
            }

            if (TryRead("!="))
            {
                return QueryOperator.NotEquals;
            }

            if (TryRead("="))
            {
                return QueryOperator.Equals;
            }

            if (TryRead(">"))
            {
                return QueryOperator.GreaterThan;
            }

            if (TryRead("<"))
            {
                return QueryOperator.LessThan;
            }

            if (TryRead("~"))
            {
                return QueryOperator.Regex;
            }

            throw new TableQueryException("Expected an operator. Supported operators are =, !=, >, >=, <, <=, and ~.");
        }

        private string ReadValue()
        {
            if (IsAtEnd)
            {
                throw new TableQueryException("Expected a value.");
            }

            if (_query[_index] == '"')
            {
                return ReadQuotedValue();
            }

            var start = _index;

            while (!IsAtEnd && !char.IsWhiteSpace(_query[_index]))
            {
                _index++;
            }

            if (start == _index)
            {
                throw new TableQueryException("Expected a value.");
            }

            return _query[start.._index];
        }

        private string ReadQuotedValue()
        {
            _index++;
            var value = new List<char>();

            while (!IsAtEnd)
            {
                var current = _query[_index++];

                if (current == '"')
                {
                    return new string(value.ToArray());
                }

                if (current == '\\' && !IsAtEnd)
                {
                    value.Add(_query[_index++]);
                    continue;
                }

                value.Add(current);
            }

            throw new TableQueryException("Quoted values must end with a quote.");
        }

        private void TryReadAnd()
        {
            var savedIndex = _index;

            if (!TryReadWord("and"))
            {
                _index = savedIndex;
            }
        }

        private bool TryReadWord(string word)
        {
            if (_query.Length - _index < word.Length)
            {
                return false;
            }

            var matches = string.Compare(_query, _index, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) == 0;
            var endsAtWordBoundary = _index + word.Length == _query.Length || char.IsWhiteSpace(_query[_index + word.Length]);

            if (!matches || !endsAtWordBoundary)
            {
                return false;
            }

            _index += word.Length;
            SkipWhiteSpace();
            return true;
        }

        private bool TryRead(string value)
        {
            if (!_query[_index..].StartsWith(value, StringComparison.Ordinal))
            {
                return false;
            }

            _index += value.Length;
            return true;
        }

        private void SkipWhiteSpace()
        {
            while (!IsAtEnd && char.IsWhiteSpace(_query[_index]))
            {
                _index++;
            }
        }
    }
}
