using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq.Expressions;

namespace DbfDataReader.Query
{
    // Translates the supported subset of LINQ expression trees into the query engine's
    // AST: comparisons, &&/||/!, == null / != null (IS [NOT] NULL),
    // string.StartsWith/EndsWith/Contains (LIKE), and collection.Contains(x.Column)
    // (IN). Everything else throws NotSupportedException - there is no silent in-memory
    // fallback. Captured variables and other row-independent subtrees are evaluated at
    // translation time and become literals.
    internal static class QueryExpressionTranslator
    {
        public static SqlExpression TranslatePredicate(LambdaExpression lambda, Func<string, int> resolveOrdinal)
        {
            return TranslateCondition(lambda.Body, lambda.Parameters[0], resolveOrdinal);
        }

        public static (int Ordinal, string Name) TranslateSortKey(LambdaExpression lambda,
            Func<string, int> resolveOrdinal)
        {
            var body = Unwrap(lambda.Body);
            if (body is MemberExpression member && IsParameterMember(member, lambda.Parameters[0]))
                return (resolveOrdinal(member.Member.Name), member.Member.Name);

            throw NotSupported(lambda.Body, "sort keys must be a property of the row type");
        }

        private static SqlExpression TranslateCondition(Expression expression, ParameterExpression parameter,
            Func<string, int> resolveOrdinal)
        {
            expression = Unwrap(expression);

            switch (expression)
            {
                case BinaryExpression binary when binary.NodeType == ExpressionType.AndAlso:
                    return new SqlBinaryExpression(SqlBinaryOperator.And,
                        TranslateCondition(binary.Left, parameter, resolveOrdinal),
                        TranslateCondition(binary.Right, parameter, resolveOrdinal));
                case BinaryExpression binary when binary.NodeType == ExpressionType.OrElse:
                    return new SqlBinaryExpression(SqlBinaryOperator.Or,
                        TranslateCondition(binary.Left, parameter, resolveOrdinal),
                        TranslateCondition(binary.Right, parameter, resolveOrdinal));
                case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                    return new SqlNotExpression(
                        TranslateCondition(unary.Operand, parameter, resolveOrdinal), 0);
                case BinaryExpression binary when TryGetComparisonOperator(binary.NodeType, out var op):
                    return TranslateComparison(op, binary, parameter, resolveOrdinal);
                case MethodCallExpression call:
                    return TranslateMethodCall(call, parameter, resolveOrdinal);
                case MemberExpression member when member.Type == typeof(bool) &&
                                                  IsParameterMember(member, parameter):
                    // a bare boolean property: x => x.Active
                    return new SqlBinaryExpression(SqlBinaryOperator.Equal,
                        Column(member, resolveOrdinal), new SqlLiteralExpression(true, 0));
                default:
                    throw NotSupported(expression, "it cannot be used as a condition");
            }
        }

        private static SqlExpression TranslateComparison(SqlBinaryOperator op, BinaryExpression binary,
            ParameterExpression parameter, Func<string, int> resolveOrdinal)
        {
            var left = TranslateOperand(binary.Left, parameter, resolveOrdinal);
            var right = TranslateOperand(binary.Right, parameter, resolveOrdinal);

            // x.Column == null keeps its LINQ meaning: translate to IS [NOT] NULL rather
            // than a three-valued comparison that never matches
            if (IsNullLiteral(right) || IsNullLiteral(left))
            {
                var operand = IsNullLiteral(right) ? left : right;
                switch (op)
                {
                    case SqlBinaryOperator.Equal:
                        return new SqlIsNullExpression(operand, false);
                    case SqlBinaryOperator.NotEqual:
                        return new SqlIsNullExpression(operand, true);
                    default:
                        throw NotSupported(binary, "only == and != can compare with null");
                }
            }

            return new SqlBinaryExpression(op, left, right);
        }

        private static SqlExpression TranslateMethodCall(MethodCallExpression call, ParameterExpression parameter,
            Func<string, int> resolveOrdinal)
        {
            if (call.Method.DeclaringType == typeof(string) && call.Object != null &&
                call.Arguments.Count == 1 && call.Arguments[0].Type == typeof(string))
            {
                var member = Unwrap(call.Object) as MemberExpression;
                if (member != null && IsParameterMember(member, parameter))
                    return TranslateStringMatch(call, member, resolveOrdinal);
            }

            if (TryGetContainsParts(call, parameter, out var collection, out var item))
            {
                var values = new List<SqlExpression>();
                foreach (var element in (IEnumerable)Evaluate(collection))
                {
                    values.Add(new SqlLiteralExpression(NormalizeLiteral(element), 0));
                }

                return new SqlInExpression(Column(item, resolveOrdinal), values, false);
            }

            throw NotSupported(call, "only string.StartsWith/EndsWith/Contains and collection.Contains are supported");
        }

        private static SqlExpression TranslateStringMatch(MethodCallExpression call, MemberExpression member,
            Func<string, int> resolveOrdinal)
        {
            if (!(Evaluate(call.Arguments[0]) is string text))
                throw NotSupported(call, "the match text must not be null");
            if (text.IndexOf('%') >= 0 || text.IndexOf('_') >= 0)
                throw NotSupported(call, "the match text must not contain '%' or '_'");

            string pattern;
            switch (call.Method.Name)
            {
                case nameof(string.StartsWith):
                    pattern = text + "%";
                    break;
                case nameof(string.EndsWith):
                    pattern = "%" + text;
                    break;
                case nameof(string.Contains):
                    pattern = "%" + text + "%";
                    break;
                default:
                    throw NotSupported(call, "only StartsWith, EndsWith and Contains are supported");
            }

            return new SqlLikeExpression(Column(member, resolveOrdinal),
                new SqlLiteralExpression(pattern, 0), false);
        }

        private static bool TryGetContainsParts(MethodCallExpression call, ParameterExpression parameter,
            out Expression collection, out MemberExpression item)
        {
            collection = null;
            item = null;

            if (call.Method.Name != nameof(IList.Contains)) return false;

            Expression collectionExpression;
            Expression itemExpression;

            if (call.Object != null && call.Arguments.Count == 1)
            {
                collectionExpression = call.Object; // list.Contains(x.Column)
                itemExpression = call.Arguments[0];
            }
            else if (call.Object == null && call.Arguments.Count == 2)
            {
                collectionExpression = call.Arguments[0]; // array.Contains(x.Column) via Enumerable
                itemExpression = call.Arguments[1];
            }
            else
            {
                return false;
            }

            if (!(Unwrap(itemExpression) is MemberExpression member) ||
                !IsParameterMember(member, parameter)) return false;

            // on newer compilers array.Contains binds to the span-based MemoryExtensions
            // overload, wrapping the collection in an op_Implicit call to ReadOnlySpan;
            // unwrap conversions to reach the underlying collection, which spans cannot
            // be evaluated as (they cannot be boxed)
            collectionExpression = Unwrap(collectionExpression);
            if (collectionExpression is MethodCallExpression conversion && conversion.Object == null &&
                conversion.Arguments.Count == 1 &&
                (conversion.Method.Name == "op_Implicit" || conversion.Method.Name == "op_Explicit"))
            {
                collectionExpression = Unwrap(conversion.Arguments[0]);
            }

            if (ContainsParameter(collectionExpression, parameter)) return false;

            collection = collectionExpression;
            item = member;
            return true;
        }

        private static SqlExpression TranslateOperand(Expression expression, ParameterExpression parameter,
            Func<string, int> resolveOrdinal)
        {
            var unwrapped = Unwrap(expression);

            if (unwrapped is MemberExpression member && IsParameterMember(member, parameter))
                return Column(member, resolveOrdinal);

            if (ContainsParameter(unwrapped, parameter))
                throw NotSupported(expression, "only direct properties of the row type can be translated");

            return new SqlLiteralExpression(NormalizeLiteral(Evaluate(unwrapped)), 0);
        }

        private static SqlColumnExpression Column(MemberExpression member, Func<string, int> resolveOrdinal)
        {
            var column = new SqlColumnExpression(member.Member.Name, 0)
            {
                Ordinal = resolveOrdinal(member.Member.Name)
            };
            return column;
        }

        private static bool IsParameterMember(MemberExpression member, ParameterExpression parameter)
        {
            return Unwrap(member.Expression) == parameter;
        }

        private static Expression Unwrap(Expression expression)
        {
            while (expression is UnaryExpression unary &&
                   (unary.NodeType == ExpressionType.Convert || unary.NodeType == ExpressionType.ConvertChecked))
            {
                expression = unary.Operand;
            }

            return expression;
        }

        private static bool TryGetComparisonOperator(ExpressionType nodeType, out SqlBinaryOperator op)
        {
            switch (nodeType)
            {
                case ExpressionType.Equal:
                    op = SqlBinaryOperator.Equal;
                    return true;
                case ExpressionType.NotEqual:
                    op = SqlBinaryOperator.NotEqual;
                    return true;
                case ExpressionType.LessThan:
                    op = SqlBinaryOperator.LessThan;
                    return true;
                case ExpressionType.LessThanOrEqual:
                    op = SqlBinaryOperator.LessThanOrEqual;
                    return true;
                case ExpressionType.GreaterThan:
                    op = SqlBinaryOperator.GreaterThan;
                    return true;
                case ExpressionType.GreaterThanOrEqual:
                    op = SqlBinaryOperator.GreaterThanOrEqual;
                    return true;
                default:
                    op = default;
                    return false;
            }
        }

        private static bool IsNullLiteral(SqlExpression expression)
        {
            return expression is SqlLiteralExpression literal && literal.Value == null;
        }

        private static object Evaluate(Expression expression)
        {
            if (expression is ConstantExpression constant) return constant.Value;

            var lambda = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object)));
            return lambda.Compile()();
        }

        private static object NormalizeLiteral(object value)
        {
            if (value is char character) return character.ToString(CultureInfo.InvariantCulture);

            return value;
        }

        private static bool ContainsParameter(Expression expression, ParameterExpression parameter)
        {
            var finder = new ParameterFinder(parameter);
            finder.Visit(expression);
            return finder.Found;
        }

        private static NotSupportedException NotSupported(Expression expression, string reason)
        {
            return new NotSupportedException(
                $"The expression '{expression}' cannot be translated to a DBF query: {reason}.");
        }

        private sealed class ParameterFinder : ExpressionVisitor
        {
            private readonly ParameterExpression _parameter;

            public ParameterFinder(ParameterExpression parameter)
            {
                _parameter = parameter;
            }

            public bool Found { get; private set; }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (node == _parameter) Found = true;

                return base.VisitParameter(node);
            }
        }
    }
}
