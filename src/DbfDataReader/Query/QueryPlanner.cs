using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DbfDataReader.Cdx;

namespace DbfDataReader.Query
{
    // Decides whether a sidecar .cdx compound index can serve a query's WHERE or
    // ORDER BY clause, and executes the index search when it can. The rules are
    // deliberately conservative - anything the planner cannot prove safe falls back to
    // a full scan, and the full WHERE expression is always re-applied as a residual
    // filter, so an index result only has to be a superset of the matching rows.
    internal static class QueryPlanner
    {
        private const byte CharacterPad = 0x20;
        private const byte BinaryPad = 0x00;

        private enum CandidateKind
        {
            Equality = 0,
            Range = 1,
            LikePrefix = 2
        }

        public static QueryAccessPlan CreatePlan(SqlExpression where,
            IReadOnlyList<(int Ordinal, bool Descending)> orderKeys, DbfTable table, bool useIndexes,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!useIndexes) return QueryAccessPlan.FullScan("indexes disabled");

            var orderOrdinal = GetSingleAscendingOrderOrdinal(orderKeys);
            if (where == null && orderOrdinal < 0) return QueryAccessPlan.FullScan("no indexable clause");

            // character keys compare byte-wise; only single-byte encodings keep that
            // order consistent with the evaluator's ordinal string comparison
            if (table.CurrentEncoding == null || !table.CurrentEncoding.IsSingleByte)
                return QueryAccessPlan.FullScan("multi-byte encoding");

            var indexPath = FindIndexPath(table.Path);
            if (indexPath == null) return QueryAccessPlan.FullScan("no index file");

            try
            {
                return CreatePlanCore(where, orderOrdinal, table, indexPath, namedParameters, positionalParameters);
            }
            catch (Exception exception) when (exception is CdxException || exception is IOException ||
                                              exception is EndOfStreamException)
            {
                return QueryAccessPlan.FullScan("index file unusable");
            }
        }

        private static QueryAccessPlan CreatePlanCore(SqlExpression where, int orderOrdinal, DbfTable table,
            string indexPath, IReadOnlyDictionary<string, object> namedParameters,
            IReadOnlyList<object> positionalParameters)
        {
            using (var cdxFile = new CdxFile(indexPath, table.CurrentEncoding))
            {
                var tags = FindEligibleTags(cdxFile, table.Columns);
                if (tags.Count == 0) return QueryAccessPlan.FullScan("no usable index tags");

                if (where != null)
                {
                    var candidate = FindBestCandidate(where, tags, table.CurrentEncoding, namedParameters,
                        positionalParameters);
                    if (candidate != null)
                    {
                        var sortSatisfied = candidate.Ordinal == orderOrdinal;
                        var recordIndexes = ExecuteSearch(candidate, sortSatisfied);
                        return QueryAccessPlan.Index(recordIndexes, sortSatisfied, candidate.Description);
                    }
                }

                if (where == null && orderOrdinal >= 0 && tags.TryGetValue(orderOrdinal, out var orderTag))
                {
                    var entries = orderTag.Index.EnumerateEntries().ToList();
                    StabilizeDuplicateKeyRuns(entries, orderTag.Index.Header.KeyLength, orderTag.PadByte);
                    var recordIndexes = ToRecordIndexes(entries, sortSatisfied: true);
                    return QueryAccessPlan.Index(recordIndexes, true, $"index order scan on tag '{orderTag.Name}'");
                }

                return QueryAccessPlan.FullScan("no matching index tag");
            }
        }

        private static int GetSingleAscendingOrderOrdinal(IReadOnlyList<(int Ordinal, bool Descending)> orderKeys)
        {
            return orderKeys != null && orderKeys.Count == 1 && !orderKeys[0].Descending
                ? orderKeys[0].Ordinal
                : -1;
        }

        private sealed class IndexTag
        {
            public IndexTag(string name, CdxIndex index, DbfColumnType columnType)
            {
                Name = name;
                Index = index;
                ColumnType = columnType;
            }

            public string Name { get; }
            public CdxIndex Index { get; }
            public DbfColumnType ColumnType { get; }

            public byte PadByte => ColumnType == DbfColumnType.Character ? CharacterPad : BinaryPad;
        }

        // A tag is usable when its key expression is a plain column of a supported
        // type, ordered ascending, and carries none of the flags that make its entries
        // a subset of the table: dBASE-style UNIQUE indexes only the first record per
        // key, and FOR clauses filter rows. Flag 0x04 (named CustomIndex here) is NOT
        // excluded: in Visual FoxPro files it marks primary/candidate keys, which
        // reject duplicate values instead of hiding records and therefore cover every
        // row. Character keys may be any length; binary keys must have the width their
        // encoding produces.
        private static Dictionary<int, IndexTag> FindEligibleTags(CdxFile cdxFile, IList<DbfColumn> columns)
        {
            var tags = new Dictionary<int, IndexTag>();

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);
                var header = index.Header;

                if (header.Order != CdxIndexOrder.Ascending) continue;
                if ((header.Options & (CdxIndexOptions.Unique | CdxIndexOptions.HasForClause)) != 0) continue;
                if (!IsPlainIdentifier(header.KeyExpression)) continue;

                var ordinal = FindColumn(columns, header.KeyExpression);
                if (ordinal < 0) continue;
                if (!IsSupportedKeyColumn(columns[ordinal].ColumnType, header.KeyLength)) continue;

                if (!tags.ContainsKey(ordinal))
                    tags.Add(ordinal, new IndexTag(tagName, index, columns[ordinal].ColumnType));
            }

            return tags;
        }

        private static bool IsSupportedKeyColumn(DbfColumnType columnType, int keyLength)
        {
            switch (columnType)
            {
                case DbfColumnType.Character:
                    return true;
                case DbfColumnType.SignedLong:
                    return keyLength == CdxKeyEncoder.IntegerKeyLength;
                case DbfColumnType.Number:
                case DbfColumnType.Float:
                case DbfColumnType.Double:
                case DbfColumnType.Date:
                    return keyLength == CdxKeyEncoder.DoubleKeyLength;
                default:
                    return false;
            }
        }

        private sealed class Candidate
        {
            public Candidate(CandidateKind kind, int ordinal, CdxIndex index, string description, byte padByte,
                byte[] equalityKey, Func<byte[], int> comparison)
            {
                Kind = kind;
                Ordinal = ordinal;
                Index = index;
                Description = description;
                PadByte = padByte;
                EqualityKey = equalityKey;
                Comparison = comparison;
            }

            public CandidateKind Kind { get; }
            public int Ordinal { get; }
            public CdxIndex Index { get; }
            public string Description { get; }
            public byte PadByte { get; }
            public byte[] EqualityKey { get; } // character equality searches
            public Func<byte[], int> Comparison { get; }

            // no key and no comparison: a provably empty result
            public static Candidate Empty(CandidateKind kind, IndexTag tag, int ordinal, string description)
            {
                return new Candidate(kind, ordinal, tag.Index, description, tag.PadByte, null, null);
            }
        }

        private static Candidate FindBestCandidate(SqlExpression where, Dictionary<int, IndexTag> tags,
            Encoding encoding, IReadOnlyDictionary<string, object> namedParameters,
            IReadOnlyList<object> positionalParameters)
        {
            Candidate best = null;

            foreach (var conjunct in FlattenConjuncts(where))
            {
                var candidate = TryCreateCandidate(conjunct, tags, encoding, namedParameters, positionalParameters);
                if (candidate == null) continue;
                if (best == null || candidate.Kind < best.Kind) best = candidate;
                if (best.Kind == CandidateKind.Equality) break;
            }

            return best;
        }

        private static IEnumerable<SqlExpression> FlattenConjuncts(SqlExpression expression)
        {
            if (expression is SqlBinaryExpression binary && binary.Operator == SqlBinaryOperator.And)
            {
                foreach (var child in FlattenConjuncts(binary.Left)) yield return child;
                foreach (var child in FlattenConjuncts(binary.Right)) yield return child;
            }
            else
            {
                yield return expression;
            }
        }

        private static Candidate TryCreateCandidate(SqlExpression conjunct, Dictionary<int, IndexTag> tags,
            Encoding encoding, IReadOnlyDictionary<string, object> namedParameters,
            IReadOnlyList<object> positionalParameters)
        {
            switch (conjunct)
            {
                case SqlBinaryExpression binary when TryGetComparison(binary, out var column, out var valueExpression,
                    out var op):
                    return TryCreateComparisonCandidate(column, valueExpression, op, tags, encoding, namedParameters,
                        positionalParameters);
                case SqlBetweenExpression between when !between.Negated &&
                                                       between.Operand is SqlColumnExpression betweenColumn:
                    return TryCreateBetweenCandidate(betweenColumn, between, tags, encoding, namedParameters,
                        positionalParameters);
                case SqlLikeExpression like when !like.Negated && like.Operand is SqlColumnExpression likeColumn:
                    return TryCreateLikeCandidate(likeColumn, like, tags, encoding, namedParameters,
                        positionalParameters);
                default:
                    return null;
            }
        }

        private static bool TryGetComparison(SqlBinaryExpression binary, out SqlColumnExpression column,
            out SqlExpression valueExpression, out SqlBinaryOperator op)
        {
            column = null;
            valueExpression = null;
            op = binary.Operator;

            if (op == SqlBinaryOperator.And || op == SqlBinaryOperator.Or || op == SqlBinaryOperator.NotEqual)
                return false;

            if (binary.Left is SqlColumnExpression leftColumn && !(binary.Right is SqlColumnExpression))
            {
                column = leftColumn;
                valueExpression = binary.Right;
                return true;
            }

            if (binary.Right is SqlColumnExpression rightColumn && !(binary.Left is SqlColumnExpression))
            {
                column = rightColumn;
                valueExpression = binary.Left;
                op = Flip(op);
                return true;
            }

            return false;
        }

        private static SqlBinaryOperator Flip(SqlBinaryOperator op)
        {
            switch (op)
            {
                case SqlBinaryOperator.LessThan: return SqlBinaryOperator.GreaterThan;
                case SqlBinaryOperator.LessThanOrEqual: return SqlBinaryOperator.GreaterThanOrEqual;
                case SqlBinaryOperator.GreaterThan: return SqlBinaryOperator.LessThan;
                case SqlBinaryOperator.GreaterThanOrEqual: return SqlBinaryOperator.LessThanOrEqual;
                default: return op;
            }
        }

        private static Candidate TryCreateComparisonCandidate(SqlColumnExpression column,
            SqlExpression valueExpression, SqlBinaryOperator op, Dictionary<int, IndexTag> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (!TryResolveValue(valueExpression, namedParameters, positionalParameters, out var value)) return null;

            switch (tag.ColumnType)
            {
                case DbfColumnType.Character:
                    return TryCreateTextComparisonCandidate(column, value, op, tag, encoding);
                case DbfColumnType.SignedLong:
                    return TryCreateIntegerComparisonCandidate(column, value, op, tag);
                case DbfColumnType.Number:
                case DbfColumnType.Float:
                case DbfColumnType.Double:
                    return TryCreateDoubleComparisonCandidate(column, value, op, tag);
                case DbfColumnType.Date:
                    return TryCreateDateComparisonCandidate(column, value, op, tag);
                default:
                    return null;
            }
        }

        private static Candidate TryCreateTextComparisonCandidate(SqlColumnExpression column, object value,
            SqlBinaryOperator op, IndexTag tag, Encoding encoding)
        {
            if (!TryGetSearchText(value, out var text)) return null;

            var keyLength = tag.Index.Header.KeyLength;
            var fits = TryPadKey(text, keyLength, encoding, out var target);

            if (op == SqlBinaryOperator.Equal)
            {
                // an equality value that cannot fit in the key is provably empty
                return fits
                    ? new Candidate(CandidateKind.Equality, column.Ordinal, tag.Index,
                        $"index seek (=) on tag '{tag.Name}'", tag.PadByte, target, null)
                    : Candidate.Empty(CandidateKind.Equality, tag, column.Ordinal,
                        $"index seek (=) on tag '{tag.Name}'");
            }

            if (!fits) return null; // over-long bound: leave it to the scan

            var comparison = CreateTextRangeComparison(op, target);
            if (comparison == null) return null;

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index,
                $"index range scan on tag '{tag.Name}'", tag.PadByte, null, comparison);
        }

        private static Func<byte[], int> CreateTextRangeComparison(SqlBinaryOperator op, byte[] target)
        {
            switch (op)
            {
                case SqlBinaryOperator.GreaterThanOrEqual:
                    return stored => CdxKeyComparer.Compare(stored, target) >= 0 ? 0 : -1;
                case SqlBinaryOperator.GreaterThan:
                    return stored => CdxKeyComparer.Compare(stored, target) > 0 ? 0 : -1;
                case SqlBinaryOperator.LessThanOrEqual:
                    return stored => CdxKeyComparer.Compare(stored, target) <= 0 ? 0 : 1;
                case SqlBinaryOperator.LessThan:
                    return stored => CdxKeyComparer.Compare(stored, target) < 0 ? 0 : 1;
                default:
                    return null;
            }
        }

        private static Candidate TryCreateIntegerComparisonCandidate(SqlColumnExpression column, object value,
            SqlBinaryOperator op, IndexTag tag)
        {
            if (!TryGetNumber(value, out var number)) return null;

            if (op == SqlBinaryOperator.Equal)
            {
                // non-integral or out-of-range values cannot equal any integer column value
                if (decimal.Truncate(number) != number || number < int.MinValue || number > int.MaxValue)
                    return Candidate.Empty(CandidateKind.Equality, tag, column.Ordinal,
                        $"index seek (=) on tag '{tag.Name}'");

                var target = CdxKeyEncoder.EncodeInteger((int)number);
                return new Candidate(CandidateKind.Equality, column.Ordinal, tag.Index,
                    $"index seek (=) on tag '{tag.Name}'", tag.PadByte, null,
                    stored => CompareBinary(stored, target));
            }

            // convert the bound to the integer domain; strict operators become
            // inclusive against the adjacent integer
            var isLowerBound = op == SqlBinaryOperator.GreaterThanOrEqual || op == SqlBinaryOperator.GreaterThan;
            var bound = AdjustIntegerBound(number, op);

            if (isLowerBound && bound > int.MaxValue)
                return Candidate.Empty(CandidateKind.Range, tag, column.Ordinal,
                    $"index range scan on tag '{tag.Name}'");
            if (!isLowerBound && bound < int.MinValue)
                return Candidate.Empty(CandidateKind.Range, tag, column.Ordinal,
                    $"index range scan on tag '{tag.Name}'");

            // a bound beyond the other end matches every row; a scan serves that better
            if (isLowerBound && bound < int.MinValue) return null;
            if (!isLowerBound && bound > int.MaxValue) return null;

            var key = CdxKeyEncoder.EncodeInteger((int)bound);
            var comparison = isLowerBound ? GreaterOrEqual(key) : LessOrEqual(key);

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index,
                $"index range scan on tag '{tag.Name}'", tag.PadByte, null, comparison);
        }

        private static decimal AdjustIntegerBound(decimal number, SqlBinaryOperator op)
        {
            switch (op)
            {
                case SqlBinaryOperator.GreaterThanOrEqual: return Math.Ceiling(number);
                case SqlBinaryOperator.GreaterThan: return Math.Floor(number) + 1;
                case SqlBinaryOperator.LessThanOrEqual: return Math.Floor(number);
                default: return Math.Ceiling(number) - 1; // LessThan
            }
        }

        private static Candidate TryCreateDoubleComparisonCandidate(SqlColumnExpression column, object value,
            SqlBinaryOperator op, IndexTag tag)
        {
            if (!TryGetNumber(value, out var number)) return null;

            return CreateDoubleKeyCandidate(column, CdxKeyEncoder.EncodeDouble((double)number), op, tag);
        }

        private static Candidate TryCreateDateComparisonCandidate(SqlColumnExpression column, object value,
            SqlBinaryOperator op, IndexTag tag)
        {
            if (!TryGetDate(value, out var date)) return null;

            return CreateDoubleKeyCandidate(column, CdxKeyEncoder.EncodeDate(date), op, tag);
        }

        private static Candidate CreateDoubleKeyCandidate(SqlColumnExpression column, byte[] target,
            SqlBinaryOperator op, IndexTag tag)
        {
            if (op == SqlBinaryOperator.Equal)
            {
                return new Candidate(CandidateKind.Equality, column.Ordinal, tag.Index,
                    $"index seek (=) on tag '{tag.Name}'", tag.PadByte, null,
                    stored => CompareBinary(stored, target));
            }

            // strict bounds stay inclusive at the key level: converting decimals to
            // doubles can collapse a strict boundary onto the bound itself, and the
            // residual filter drops the boundary rows exactly
            var isLowerBound = op == SqlBinaryOperator.GreaterThanOrEqual || op == SqlBinaryOperator.GreaterThan;
            var comparison = isLowerBound ? GreaterOrEqual(target) : LessOrEqual(target);

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index,
                $"index range scan on tag '{tag.Name}'", tag.PadByte, null, comparison);
        }

        private static Candidate TryCreateBetweenCandidate(SqlColumnExpression column, SqlBetweenExpression between,
            Dictionary<int, IndexTag> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (!TryResolveValue(between.Low, namedParameters, positionalParameters, out var lowValue)) return null;
            if (!TryResolveValue(between.High, namedParameters, positionalParameters, out var highValue)) return null;

            var description = $"index range scan (between) on tag '{tag.Name}'";

            switch (tag.ColumnType)
            {
                case DbfColumnType.Character:
                    return TryCreateTextBetweenCandidate(column, lowValue, highValue, tag, encoding, description);
                case DbfColumnType.SignedLong:
                    return TryCreateIntegerBetweenCandidate(column, lowValue, highValue, tag, description);
                case DbfColumnType.Number:
                case DbfColumnType.Float:
                case DbfColumnType.Double:
                    return TryGetNumber(lowValue, out var lowNumber) && TryGetNumber(highValue, out var highNumber)
                        ? CreateBinaryBetweenCandidate(column, CdxKeyEncoder.EncodeDouble((double)lowNumber),
                            CdxKeyEncoder.EncodeDouble((double)highNumber), tag, description)
                        : null;
                case DbfColumnType.Date:
                    return TryGetDate(lowValue, out var lowDate) && TryGetDate(highValue, out var highDate)
                        ? CreateBinaryBetweenCandidate(column, CdxKeyEncoder.EncodeDate(lowDate),
                            CdxKeyEncoder.EncodeDate(highDate), tag, description)
                        : null;
                default:
                    return null;
            }
        }

        private static Candidate TryCreateTextBetweenCandidate(SqlColumnExpression column, object lowValue,
            object highValue, IndexTag tag, Encoding encoding, string description)
        {
            if (!TryGetSearchText(lowValue, out var lowText)) return null;
            if (!TryGetSearchText(highValue, out var highText)) return null;

            var keyLength = tag.Index.Header.KeyLength;
            if (!TryPadKey(lowText, keyLength, encoding, out var low)) return null;
            if (!TryPadKey(highText, keyLength, encoding, out var high)) return null;

            int Comparison(byte[] stored)
            {
                if (CdxKeyComparer.Compare(stored, low) < 0) return -1;
                return CdxKeyComparer.Compare(stored, high) > 0 ? 1 : 0;
            }

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index, description, tag.PadByte, null,
                Comparison);
        }

        private static Candidate TryCreateIntegerBetweenCandidate(SqlColumnExpression column, object lowValue,
            object highValue, IndexTag tag, string description)
        {
            if (!TryGetNumber(lowValue, out var lowNumber)) return null;
            if (!TryGetNumber(highValue, out var highNumber)) return null;

            var low = Math.Ceiling(lowNumber);
            var high = Math.Floor(highNumber);

            if (low > high || low > int.MaxValue || high < int.MinValue)
                return Candidate.Empty(CandidateKind.Range, tag, column.Ordinal, description);

            var lowKey = CdxKeyEncoder.EncodeInteger((int)Math.Max(low, int.MinValue));
            var highKey = CdxKeyEncoder.EncodeInteger((int)Math.Min(high, int.MaxValue));

            return CreateBinaryBetweenCandidate(column, lowKey, highKey, tag, description);
        }

        private static Candidate CreateBinaryBetweenCandidate(SqlColumnExpression column, byte[] low, byte[] high,
            IndexTag tag, string description)
        {
            int Comparison(byte[] stored)
            {
                if (CompareBinary(stored, low) < 0) return -1;
                return CompareBinary(stored, high) > 0 ? 1 : 0;
            }

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index, description, tag.PadByte, null,
                Comparison);
        }

        private static Candidate TryCreateLikeCandidate(SqlColumnExpression column, SqlLikeExpression like,
            Dictionary<int, IndexTag> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (tag.ColumnType != DbfColumnType.Character) return null;
            if (!TryResolveValue(like.Pattern, namedParameters, positionalParameters, out var value)) return null;
            if (!TryGetSearchText(value, out var pattern)) return null;

            var wildcardIndex = pattern.IndexOfAny(new[] { '%', '_' });
            if (wildcardIndex <= 0) return null; // no usable prefix

            var prefix = pattern.Substring(0, wildcardIndex);
            var prefixBytes = encoding.GetBytes(prefix);
            var keyLength = tag.Index.Header.KeyLength;

            if (prefixBytes.Length > keyLength)
            {
                // the column can never hold a value starting with a prefix longer than
                // the key; provably empty
                return Candidate.Empty(CandidateKind.LikePrefix, tag, column.Ordinal,
                    $"index prefix scan on tag '{tag.Name}' (impossible prefix)");
            }

            return new Candidate(CandidateKind.LikePrefix, column.Ordinal, tag.Index,
                $"index prefix scan (like) on tag '{tag.Name}'", tag.PadByte, null,
                stored => CdxKeyComparer.Compare(stored, prefixBytes));
        }

        private static int CompareBinary(byte[] stored, byte[] target)
        {
            return CdxKeyComparer.Compare(stored, target, BinaryPad);
        }

        private static Func<byte[], int> GreaterOrEqual(byte[] target)
        {
            return stored => CompareBinary(stored, target) >= 0 ? 0 : -1;
        }

        private static Func<byte[], int> LessOrEqual(byte[] target)
        {
            return stored => CompareBinary(stored, target) <= 0 ? 0 : 1;
        }

        private static bool TryResolveValue(SqlExpression expression,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters,
            out object value)
        {
            value = null;

            switch (expression)
            {
                case SqlLiteralExpression literal:
                    value = literal.Value;
                    break;
                case SqlParameterExpression parameter when parameter.Name != null:
                    if (namedParameters == null || !namedParameters.TryGetValue(parameter.Name, out value))
                        return false;
                    break;
                case SqlParameterExpression parameter:
                    if (positionalParameters == null || parameter.Index >= positionalParameters.Count) return false;
                    value = positionalParameters[parameter.Index];
                    break;
                default:
                    return false;
            }

            if (value is char character) value = character.ToString();
            return value != null;
        }

        // character search text must be printable ASCII; that keeps byte order and the
        // evaluator's ordinal comparison sign-consistent
        private static bool TryGetSearchText(object value, out string text)
        {
            text = null;

            if (!(value is string candidate)) return false;
            if (!candidate.All(c => c >= 0x20 && c <= 0x7E)) return false;

            text = candidate;
            return true;
        }

        private static bool TryGetNumber(object value, out decimal number)
        {
            number = 0;

            switch (value)
            {
                case byte _:
                case sbyte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                case decimal _:
                    number = Convert.ToDecimal(value, System.Globalization.CultureInfo.InvariantCulture);
                    return true;
                case float _:
                case double _:
                    var floating = Convert.ToDouble(value, System.Globalization.CultureInfo.InvariantCulture);
                    if (double.IsNaN(floating) || floating < (double)decimal.MinValue ||
                        floating > (double)decimal.MaxValue) return false;
                    number = (decimal)floating;
                    return true;
                default:
                    return false;
            }
        }

        private static bool TryGetDate(object value, out DateTime date)
        {
            switch (value)
            {
                case DateTime dateTime:
                    date = dateTime;
                    return true;
                case string text:
                    return SqlValueComparer.TryParseDateTime(text, out date);
                default:
                    date = default;
                    return false;
            }
        }

        // trims trailing spaces, which the dialect ignores, and pads the value to the
        // key length. Fails when the trimmed value cannot fit in a key.
        private static bool TryPadKey(string text, int keyLength, Encoding encoding, out byte[] key)
        {
            key = null;

            var bytes = encoding.GetBytes(text.TrimEnd(' '));
            if (bytes.Length > keyLength) return false;

            if (bytes.Length == keyLength)
            {
                key = bytes;
                return true;
            }

            var padded = new byte[keyLength];
            Array.Copy(bytes, padded, bytes.Length);
            for (var i = bytes.Length; i < keyLength; i++) padded[i] = CharacterPad;

            key = padded;
            return true;
        }

        private static IReadOnlyList<int> ExecuteSearch(Candidate candidate, bool sortSatisfied)
        {
            List<CdxKeyEntry> entries;

            if (candidate.Comparison != null)
            {
                entries = candidate.Index.Search(candidate.Comparison).ToList();
            }
            else if (candidate.EqualityKey != null)
            {
                entries = candidate.Index.Search(candidate.EqualityKey).ToList();
            }
            else
            {
                entries = new List<CdxKeyEntry>();
            }

            StabilizeDuplicateKeyRuns(entries, candidate.Index.Header.KeyLength, candidate.PadByte);
            return ToRecordIndexes(entries, sortSatisfied);
        }

        // entries with equal keys carry no defined order in the index; sorting each run
        // by record index makes index order identical to a stable scan-and-sort
        private static void StabilizeDuplicateKeyRuns(List<CdxKeyEntry> entries, int keyLength, byte padByte)
        {
            var start = 0;
            for (var i = 1; i <= entries.Count; i++)
            {
                if (i < entries.Count && KeysEqual(entries[start], entries[i], keyLength, padByte)) continue;

                if (i - start > 1)
                    entries.Sort(start, i - start,
                        Comparer<CdxKeyEntry>.Create((x, y) => x.RecordIndex.CompareTo(y.RecordIndex)));

                start = i;
            }
        }

        private static bool KeysEqual(CdxKeyEntry x, CdxKeyEntry y, int keyLength, byte padByte)
        {
            var target = PadKeyBytes(y.KeyBytes, keyLength, padByte);
            return CdxKeyComparer.Compare(x.KeyBytes, target, padByte) == 0;
        }

        private static byte[] PadKeyBytes(byte[] bytes, int keyLength, byte padByte)
        {
            if (bytes.Length >= keyLength) return bytes;

            var padded = new byte[keyLength];
            Array.Copy(bytes, padded, bytes.Length);
            for (var i = bytes.Length; i < keyLength; i++) padded[i] = padByte;

            return padded;
        }

        private static IReadOnlyList<int> ToRecordIndexes(List<CdxKeyEntry> entries, bool sortSatisfied)
        {
            var recordIndexes = entries.Select(entry => entry.RecordIndex).Where(index => index >= 0).ToList();

            // when the caller sorts anyway, reading in record order is better for I/O
            if (!sortSatisfied) recordIndexes.Sort();

            return recordIndexes;
        }

        private static bool IsPlainIdentifier(string keyExpression)
        {
            if (string.IsNullOrEmpty(keyExpression)) return false;
            if (!char.IsLetter(keyExpression[0]) && keyExpression[0] != '_') return false;

            return keyExpression.All(c => char.IsLetterOrDigit(c) || c == '_');
        }

        private static int FindColumn(IList<DbfColumn> columns, string name)
        {
            for (var ordinal = 0; ordinal < columns.Count; ordinal++)
            {
                if (string.Equals(columns[ordinal].ColumnName, name, StringComparison.OrdinalIgnoreCase))
                    return ordinal;
            }

            return -1;
        }

        private static string FindIndexPath(string dbfPath)
        {
            if (string.IsNullOrEmpty(dbfPath)) return null;

            var paths = new[]
            {
                Path.ChangeExtension(dbfPath, "cdx"),
                Path.ChangeExtension(dbfPath, "CDX")
            };

            return paths.FirstOrDefault(File.Exists);
        }
    }
}
