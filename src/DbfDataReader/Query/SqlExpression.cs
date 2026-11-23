using System.Collections.Generic;

namespace DbfDataReader.Query
{
    internal abstract class SqlExpression
    {
        protected SqlExpression(int position)
        {
            Position = position;
        }

        // zero-based character position within the command text, for error messages
        public int Position { get; }
    }

    internal enum SqlBinaryOperator
    {
        Or,
        And,
        Equal,
        NotEqual,
        LessThan,
        LessThanOrEqual,
        GreaterThan,
        GreaterThanOrEqual
    }

    internal sealed class SqlBinaryExpression : SqlExpression
    {
        public SqlBinaryExpression(SqlBinaryOperator op, SqlExpression left, SqlExpression right)
            : base(left.Position)
        {
            Operator = op;
            Left = left;
            Right = right;
        }

        public SqlBinaryOperator Operator { get; }

        public SqlExpression Left { get; }

        public SqlExpression Right { get; }
    }

    internal sealed class SqlNotExpression : SqlExpression
    {
        public SqlNotExpression(SqlExpression operand, int position)
            : base(position)
        {
            Operand = operand;
        }

        public SqlExpression Operand { get; }
    }

    internal sealed class SqlBetweenExpression : SqlExpression
    {
        public SqlBetweenExpression(SqlExpression operand, SqlExpression low, SqlExpression high, bool negated)
            : base(operand.Position)
        {
            Operand = operand;
            Low = low;
            High = high;
            Negated = negated;
        }

        public SqlExpression Operand { get; }

        public SqlExpression Low { get; }

        public SqlExpression High { get; }

        public bool Negated { get; }
    }

    internal sealed class SqlInExpression : SqlExpression
    {
        public SqlInExpression(SqlExpression operand, IReadOnlyList<SqlExpression> values, bool negated)
            : base(operand.Position)
        {
            Operand = operand;
            Values = values;
            Negated = negated;
        }

        public SqlExpression Operand { get; }

        public IReadOnlyList<SqlExpression> Values { get; }

        public bool Negated { get; }
    }

    internal sealed class SqlLikeExpression : SqlExpression
    {
        public SqlLikeExpression(SqlExpression operand, SqlExpression pattern, bool negated)
            : base(operand.Position)
        {
            Operand = operand;
            Pattern = pattern;
            Negated = negated;
        }

        public SqlExpression Operand { get; }

        public SqlExpression Pattern { get; }

        public bool Negated { get; }
    }

    internal sealed class SqlIsNullExpression : SqlExpression
    {
        public SqlIsNullExpression(SqlExpression operand, bool negated)
            : base(operand.Position)
        {
            Operand = operand;
            Negated = negated;
        }

        public SqlExpression Operand { get; }

        public bool Negated { get; }
    }

    internal sealed class SqlColumnExpression : SqlExpression
    {
        public SqlColumnExpression(string name, int position)
            : base(position)
        {
            Name = name;
        }

        public string Name { get; }

        // resolved by SqlBinder; -1 until bound
        public int Ordinal { get; set; } = -1;
    }

    internal sealed class SqlLiteralExpression : SqlExpression
    {
        public SqlLiteralExpression(object value, int position)
            : base(position)
        {
            Value = value;
        }

        // string, decimal, bool, or null
        public object Value { get; }
    }

    internal sealed class SqlParameterExpression : SqlExpression
    {
        public SqlParameterExpression(string name, int index, int position)
            : base(position)
        {
            Name = name;
            Index = index;
        }

        // null for positional parameters
        public string Name { get; }

        // zero-based ordinal among positional parameters; -1 for named parameters
        public int Index { get; }
    }
}
