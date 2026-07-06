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
        private const byte Pad = 0x20;

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

            // index keys compare byte-wise; only single-byte encodings keep that order
            // consistent with the evaluator's ordinal string comparison
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
                    StabilizeDuplicateKeyRuns(entries, orderTag.Index.Header.KeyLength);
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

        // A tag is usable when its key expression is a plain character column, ordered
        // ascending, and carries none of the flags that make its entries a subset of
        // the table: dBASE-style UNIQUE indexes only the first record per key, and FOR
        // clauses filter rows. Flag 0x04 (named CustomIndex here) is NOT excluded: in
        // Visual FoxPro files it marks primary/candidate keys, which reject duplicate
        // values instead of hiding records and therefore cover every row.
        private static Dictionary<int, (string Name, CdxIndex Index)> FindEligibleTags(CdxFile cdxFile,
            IList<DbfColumn> columns)
        {
            var tags = new Dictionary<int, (string, CdxIndex)>();

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);
                var header = index.Header;

                if (header.Order != CdxIndexOrder.Ascending) continue;
                if ((header.Options & (CdxIndexOptions.Unique | CdxIndexOptions.HasForClause)) != 0) continue;
                if (!IsPlainIdentifier(header.KeyExpression)) continue;

                var ordinal = FindColumn(columns, header.KeyExpression);
                if (ordinal < 0 || columns[ordinal].ColumnType != DbfColumnType.Character) continue;

                if (!tags.ContainsKey(ordinal)) tags.Add(ordinal, (tagName, index));
            }

            return tags;
        }

        private sealed class Candidate
        {
            public Candidate(CandidateKind kind, int ordinal, CdxIndex index, string description,
                byte[] equalityKey, Func<byte[], int> comparison)
            {
                Kind = kind;
                Ordinal = ordinal;
                Index = index;
                Description = description;
                EqualityKey = equalityKey;
                Comparison = comparison;
            }

            public CandidateKind Kind { get; }
            public int Ordinal { get; }
            public CdxIndex Index { get; }
            public string Description { get; }
            public byte[] EqualityKey { get; } // null for an impossible equality (over-long key)
            public Func<byte[], int> Comparison { get; }
        }

        private static Candidate FindBestCandidate(SqlExpression where,
            Dictionary<int, (string Name, CdxIndex Index)> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
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

        private static Candidate TryCreateCandidate(SqlExpression conjunct,
            Dictionary<int, (string Name, CdxIndex Index)> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
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
            SqlExpression valueExpression, SqlBinaryOperator op,
            Dictionary<int, (string Name, CdxIndex Index)> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (!TryResolveSearchText(valueExpression, namedParameters, positionalParameters, out var text))
                return null;

            var keyLength = tag.Index.Header.KeyLength;
            var fits = TryPadKey(text, keyLength, encoding, out var target);

            if (op == SqlBinaryOperator.Equal)
            {
                // an equality value that cannot fit in the key is provably empty
                return new Candidate(CandidateKind.Equality, column.Ordinal, tag.Index,
                    $"index seek (=) on tag '{tag.Name}'", fits ? target : null, null);
            }

            if (!fits) return null; // over-long bound: leave it to the scan

            var comparison = CreateRangeComparison(op, target);
            if (comparison == null) return null;

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index,
                $"index range scan on tag '{tag.Name}'", null, comparison);
        }

        private static Func<byte[], int> CreateRangeComparison(SqlBinaryOperator op, byte[] target)
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

        private static Candidate TryCreateBetweenCandidate(SqlColumnExpression column, SqlBetweenExpression between,
            Dictionary<int, (string Name, CdxIndex Index)> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (!TryResolveSearchText(between.Low, namedParameters, positionalParameters, out var lowText))
                return null;
            if (!TryResolveSearchText(between.High, namedParameters, positionalParameters, out var highText))
                return null;

            var keyLength = tag.Index.Header.KeyLength;
            if (!TryPadKey(lowText, keyLength, encoding, out var low)) return null;
            if (!TryPadKey(highText, keyLength, encoding, out var high)) return null;

            int Comparison(byte[] stored)
            {
                if (CdxKeyComparer.Compare(stored, low) < 0) return -1;
                return CdxKeyComparer.Compare(stored, high) > 0 ? 1 : 0;
            }

            return new Candidate(CandidateKind.Range, column.Ordinal, tag.Index,
                $"index range scan (between) on tag '{tag.Name}'", null, Comparison);
        }

        private static Candidate TryCreateLikeCandidate(SqlColumnExpression column, SqlLikeExpression like,
            Dictionary<int, (string Name, CdxIndex Index)> tags, Encoding encoding,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters)
        {
            if (!tags.TryGetValue(column.Ordinal, out var tag)) return null;
            if (!TryResolveSearchText(like.Pattern, namedParameters, positionalParameters, out var pattern))
                return null;

            var wildcardIndex = pattern.IndexOfAny(new[] { '%', '_' });
            if (wildcardIndex <= 0) return null; // no usable prefix

            var prefix = pattern.Substring(0, wildcardIndex);
            var prefixBytes = encoding.GetBytes(prefix);
            var keyLength = tag.Index.Header.KeyLength;

            if (prefixBytes.Length > keyLength)
            {
                // the column can never hold a value starting with a prefix longer than
                // the key; provably empty
                return new Candidate(CandidateKind.LikePrefix, column.Ordinal, tag.Index,
                    $"index prefix scan on tag '{tag.Name}' (impossible prefix)", null,
                    null);
            }

            return new Candidate(CandidateKind.LikePrefix, column.Ordinal, tag.Index,
                $"index prefix scan (like) on tag '{tag.Name}'", null,
                stored => CdxKeyComparer.Compare(stored, prefixBytes));
        }

        // the search text must be a string whose characters are printable ASCII; that
        // keeps byte order and the evaluator's ordinal comparison sign-consistent
        private static bool TryResolveSearchText(SqlExpression expression,
            IReadOnlyDictionary<string, object> namedParameters, IReadOnlyList<object> positionalParameters,
            out string text)
        {
            text = null;
            object value;

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
            if (!(value is string candidate)) return false;
            if (!candidate.All(c => c >= 0x20 && c <= 0x7E)) return false;

            text = candidate;
            return true;
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
            for (var i = bytes.Length; i < keyLength; i++) padded[i] = Pad;

            key = padded;
            return true;
        }

        private static IReadOnlyList<int> ExecuteSearch(Candidate candidate, bool sortSatisfied)
        {
            List<CdxKeyEntry> entries;

            if (candidate.Kind == CandidateKind.Equality)
            {
                // an equality key that cannot fit in the index key is provably empty
                entries = candidate.EqualityKey == null
                    ? new List<CdxKeyEntry>()
                    : candidate.Index.Search(candidate.EqualityKey).ToList();
            }
            else if (candidate.Comparison == null)
            {
                entries = new List<CdxKeyEntry>();
            }
            else
            {
                entries = candidate.Index.Search(candidate.Comparison).ToList();
            }

            StabilizeDuplicateKeyRuns(entries, candidate.Index.Header.KeyLength);
            return ToRecordIndexes(entries, sortSatisfied);
        }

        // entries with equal keys carry no defined order in the index; sorting each run
        // by record index makes index order identical to a stable scan-and-sort
        private static void StabilizeDuplicateKeyRuns(List<CdxKeyEntry> entries, int keyLength)
        {
            var start = 0;
            for (var i = 1; i <= entries.Count; i++)
            {
                if (i < entries.Count && KeysEqual(entries[start], entries[i], keyLength)) continue;

                if (i - start > 1)
                    entries.Sort(start, i - start,
                        Comparer<CdxKeyEntry>.Create((x, y) => x.RecordIndex.CompareTo(y.RecordIndex)));

                start = i;
            }
        }

        private static bool KeysEqual(CdxKeyEntry x, CdxKeyEntry y, int keyLength)
        {
            var target = TryPadKeyBytes(y.KeyBytes, keyLength);
            return CdxKeyComparer.Compare(x.KeyBytes, target) == 0;
        }

        private static byte[] TryPadKeyBytes(byte[] bytes, int keyLength)
        {
            if (bytes.Length >= keyLength) return bytes;

            var padded = new byte[keyLength];
            Array.Copy(bytes, padded, bytes.Length);
            for (var i = bytes.Length; i < keyLength; i++) padded[i] = Pad;

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
