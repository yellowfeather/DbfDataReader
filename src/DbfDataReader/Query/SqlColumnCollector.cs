using System.Collections.Generic;

namespace DbfDataReader.Query
{
    // collects the ordinals of every column a bound expression references, so the
    // engine can parse only the values a query actually needs (issue #296)
    internal static class SqlColumnCollector
    {
        public static HashSet<int> CollectOrdinals(SqlExpression expression)
        {
            var ordinals = new HashSet<int>();
            Collect(expression, ordinals);
            return ordinals;
        }

        public static int[] ToSortedOrdinals(HashSet<int> ordinals)
        {
            var result = new int[ordinals.Count];
            ordinals.CopyTo(result);
            System.Array.Sort(result);
            return result;
        }

        private static void Collect(SqlExpression expression, HashSet<int> ordinals)
        {
            switch (expression)
            {
                case null:
                    return;
                case SqlColumnExpression column:
                    ordinals.Add(column.Ordinal);
                    return;
                case SqlBinaryExpression binary:
                    Collect(binary.Left, ordinals);
                    Collect(binary.Right, ordinals);
                    return;
                case SqlNotExpression not:
                    Collect(not.Operand, ordinals);
                    return;
                case SqlBetweenExpression between:
                    Collect(between.Operand, ordinals);
                    Collect(between.Low, ordinals);
                    Collect(between.High, ordinals);
                    return;
                case SqlInExpression inExpression:
                    Collect(inExpression.Operand, ordinals);
                    foreach (var value in inExpression.Values)
                    {
                        Collect(value, ordinals);
                    }

                    return;
                case SqlLikeExpression like:
                    Collect(like.Operand, ordinals);
                    Collect(like.Pattern, ordinals);
                    return;
                case SqlIsNullExpression isNull:
                    Collect(isNull.Operand, ordinals);
                    return;
                default:
                    return; // literals and parameters reference no columns
            }
        }
    }
}
