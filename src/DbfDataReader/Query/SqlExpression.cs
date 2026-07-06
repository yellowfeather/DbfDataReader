using System.Collections.Generic;

namespace DbfDataReader.Query
{
    internal abstract class SqlExpression
    {
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
        public SqlNotExpression(SqlExpression operand)
        {
            Operand = operand;
        }

        public SqlExpression Operand { get; }
    }

    internal sealed class SqlBetweenExpression : SqlExpression
    {
        public SqlBetweenExpression(SqlExpression operand, SqlExpression low, SqlExpression high, bool negated)
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
        {
            Name = name;
            Position = position;
        }

        public string Name { get; }

        public int Position { get; }

        // resolved by SqlBinder; -1 until bound
        public int Ordinal { get; set; } = -1;
    }

    internal sealed class SqlLiteralExpression : SqlExpression
    {
        public SqlLiteralExpression(object value)
        {
            Value = value;
        }

        // string, decimal, bool, or null
        public object Value { get; }
    }

    internal sealed class SqlParameterExpression : SqlExpression
    {
        public SqlParameterExpression(string name, int index, int position)
        {
            Name = name;
            Index = index;
            Position = position;
        }

        // null for positional parameters
        public string Name { get; }

        // zero-based ordinal among positional parameters; -1 for named parameters
        public int Index { get; }

        public int Position { get; }
    }
}
