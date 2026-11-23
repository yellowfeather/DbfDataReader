namespace DbfDataReader.Query
{
    internal enum SqlTokenType
    {
        Identifier,
        QuotedIdentifier,
        BracketedIdentifier,
        StringLiteral,
        NumberLiteral,
        NamedParameter,
        PositionalParameter,

        Comma,
        LeftParen,
        RightParen,
        Star,
        Semicolon,
        Minus,

        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual,

        SelectKeyword,
        TopKeyword,
        AsKeyword,
        FromKeyword,
        WhereKeyword,
        OrderKeyword,
        ByKeyword,
        AscKeyword,
        DescKeyword,
        LimitKeyword,
        AndKeyword,
        OrKeyword,
        NotKeyword,
        BetweenKeyword,
        InKeyword,
        LikeKeyword,
        IsKeyword,
        NullKeyword,
        TrueKeyword,
        FalseKeyword,

        EndOfText
    }

    internal readonly struct SqlToken
    {
        public SqlToken(SqlTokenType type, string value, int position)
        {
            Type = type;
            Value = value;
            Position = position;
        }

        public SqlTokenType Type { get; }

        public string Value { get; }

        public int Position { get; }
    }
}
