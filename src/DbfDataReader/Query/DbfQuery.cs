using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using DbfDataReader.Query;

namespace DbfDataReader
{
    // A typed query over a DbfTable. Where/OrderBy lambdas are translated into the same
    // engine that executes SQL text; anything that cannot be translated throws
    // NotSupportedException. The type's public settable properties define which columns
    // are read, matched by name (exact first, then case-insensitively). Each enumeration
    // rewinds the table, so the underlying stream must be seekable; enumerations must
    // not overlap.
    public sealed class DbfQuery<T> : IEnumerable<T> where T : class
    {
        private readonly DbfTable _table;
        private readonly List<LambdaExpression> _predicates = new List<LambdaExpression>();
        private readonly List<(LambdaExpression Key, bool Descending)> _orderBy =
            new List<(LambdaExpression, bool)>();
        private int? _take;
        private bool _includeDeleted;
        private bool _useIndexes = true;

        internal DbfQuery(DbfTable table)
        {
            _table = table;
        }

        // multiple Where calls are combined with AND
        public DbfQuery<T> Where(Expression<Func<T, bool>> predicate)
        {
            if (predicate == null) throw new ArgumentNullException(nameof(predicate));

            _predicates.Add(predicate);
            return this;
        }

        // each OrderBy/OrderByDescending call appends a sort key
        public DbfQuery<T> OrderBy<TKey>(Expression<Func<T, TKey>> key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            _orderBy.Add((key, false));
            return this;
        }

        public DbfQuery<T> OrderByDescending<TKey>(Expression<Func<T, TKey>> key)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));

            _orderBy.Add((key, true));
            return this;
        }

        public DbfQuery<T> Take(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            _take = count;
            return this;
        }

        // deleted records are skipped by default
        public DbfQuery<T> IncludeDeleted()
        {
            _includeDeleted = true;
            return this;
        }

        // by default a sidecar compound index file is used automatically when it can
        // serve the query. Calling this method forces a sequential table scan.
        public DbfQuery<T> WithoutIndexes()
        {
            _useIndexes = false;
            return this;
        }

        // describes how the query would fetch its rows, without reading any
        public string ExplainPlan()
        {
            var plan = Prepare();
            var sortNote = plan.SortKeys.Count > 0 && !plan.AccessPlan.SortSatisfied ? "; in-memory sort" : "";
            return plan.AccessPlan.Description + sortNote;
        }

        public IEnumerator<T> GetEnumerator()
        {
            return Execute().GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public List<T> ToList()
        {
            var results = new List<T>();
            foreach (var item in this)
            {
                results.Add(item);
            }

            return results;
        }

        public T First()
        {
            using (var enumerator = GetEnumerator())
            {
                if (enumerator.MoveNext()) return enumerator.Current;
            }

            throw new InvalidOperationException("The query returned no rows.");
        }

        public T FirstOrDefault()
        {
            using (var enumerator = GetEnumerator())
            {
                return enumerator.MoveNext() ? enumerator.Current : null;
            }
        }

        public int Count()
        {
            var plan = Prepare();
            var record = new DbfRecord(_table);
            record.EnableSubsetParsing();

            _table.Seek(0);

            var limit = _take ?? int.MaxValue;
            var recordIndexes = plan.AccessPlan.RecordIndexes;

            // an index result that is exactly the matching rows (or any index result
            // when there is no filter) can be counted without reading row values
            var indexCountsRows = recordIndexes != null &&
                                  (plan.Filter == null || plan.AccessPlan.CoversWhereExactly);
            if (indexCountsRows)
            {
                return _includeDeleted
                    ? Math.Min(recordIndexes.Count, limit)
                    : CountWithStatusChecks(recordIndexes, record, limit);
            }

            // no filter: a status-only scan skips value parsing entirely
            if (plan.Filter == null) return CountByStatusScan(record, limit);

            return CountByReadingRows(plan, record, limit);
        }

        private int CountWithStatusChecks(IReadOnlyList<int> recordIndexes, DbfRecord record, int limit)
        {
            var count = 0;
            foreach (var recordIndex in recordIndexes)
            {
                if (count >= limit) break;

                _table.Seek(recordIndex);
                if (!_table.ReadRaw(record) || record.IsDeleted) continue;

                count++;
            }

            return count;
        }

        private int CountByStatusScan(DbfRecord record, int limit)
        {
            var count = 0;
            while (count < limit && _table.ReadRaw(record))
            {
                if (!_includeDeleted && record.IsDeleted) continue;

                count++;
            }

            return count;
        }

        private int CountByReadingRows(QueryPlan plan, DbfRecord record, int limit)
        {
            Func<int, object> accessor = record.GetValue;

            var position = 0;
            var count = 0;
            while (count < limit && ReadNextRow(plan, record, ref position))
            {
                if (!MatchesFilter(record, plan, accessor)) continue;

                count++;
            }

            return count;
        }

        // advances to the next candidate row: sequentially, or by seeking the next
        // record index supplied by the access plan
        private bool ReadNextRow(QueryPlan plan, DbfRecord record, ref int position)
        {
            var recordIndexes = plan.AccessPlan.RecordIndexes;
            if (recordIndexes == null) return _table.ReadRaw(record);

            while (position < recordIndexes.Count)
            {
                _table.Seek(recordIndexes[position]);
                position++;
                if (_table.ReadRaw(record)) return true; // false: entry beyond the table
            }

            return false;
        }

        public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
        {
            var results = new List<T>();
            await foreach (var item in AsAsyncEnumerable(cancellationToken).ConfigureAwait(false))
            {
                results.Add(item);
            }

            return results;
        }

        public async IAsyncEnumerable<T> AsAsyncEnumerable(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var plan = Prepare();
            var record = new DbfRecord(_table);
            record.EnableSubsetParsing();
            Func<int, object> accessor = record.GetValue;
            var cursor = new RowCursor();

            _table.Seek(0);

            if (!SortRequired(plan))
            {
                var returned = 0;
                while (NotLimited(returned) &&
                       await ReadNextRowAsync(plan, record, cursor, cancellationToken).ConfigureAwait(false))
                {
                    var decision = EvaluateRow(record, plan, accessor);
                    if (decision == RowDecision.Stop) break;
                    if (decision == RowDecision.Skip) continue;

                    yield return Materialize(record, plan);
                    returned++;
                }

                yield break;
            }

            var buffer = await BuildSortBufferAsync(plan, record, accessor, cursor, cancellationToken)
                .ConfigureAwait(false);
            foreach (var item in SortAndLimit(buffer, plan))
            {
                yield return item;
            }
        }

        private async Task<List<(T Item, object[] Keys)>> BuildSortBufferAsync(QueryPlan plan, DbfRecord record,
            Func<int, object> accessor, RowCursor cursor, CancellationToken cancellationToken)
        {
            var buffer = new List<(T Item, object[] Keys)>();
            while (await ReadNextRowAsync(plan, record, cursor, cancellationToken).ConfigureAwait(false))
            {
                var decision = EvaluateRow(record, plan, accessor);
                if (decision == RowDecision.Stop) break;
                if (decision == RowDecision.Skip) continue;

                buffer.Add((Materialize(record, plan), SnapshotSortKeys(record, plan)));
            }

            return buffer;
        }

        private sealed class RowCursor
        {
            public int Position;
        }

        private async Task<bool> ReadNextRowAsync(QueryPlan plan, DbfRecord record, RowCursor cursor,
            CancellationToken cancellationToken)
        {
            var recordIndexes = plan.AccessPlan.RecordIndexes;
            if (recordIndexes == null)
                return await _table.ReadRawAsync(record, cancellationToken).ConfigureAwait(false);

            while (cursor.Position < recordIndexes.Count)
            {
                _table.Seek(recordIndexes[cursor.Position]);
                cursor.Position++;
                if (await _table.ReadRawAsync(record, cancellationToken).ConfigureAwait(false)) return true;
            }

            return false;
        }

        private IEnumerable<T> Execute()
        {
            var plan = Prepare();
            var record = new DbfRecord(_table);
            record.EnableSubsetParsing();
            Func<int, object> accessor = record.GetValue;

            _table.Seek(0);

            if (!SortRequired(plan))
            {
                var position = 0;
                var returned = 0;
                while (NotLimited(returned) && ReadNextRow(plan, record, ref position))
                {
                    var decision = EvaluateRow(record, plan, accessor);
                    if (decision == RowDecision.Stop) break;
                    if (decision == RowDecision.Skip) continue;

                    yield return Materialize(record, plan);
                    returned++;
                }

                yield break;
            }

            foreach (var item in SortAndLimit(BuildSortBuffer(plan, record, accessor), plan))
            {
                yield return item;
            }
        }

        private List<(T Item, object[] Keys)> BuildSortBuffer(QueryPlan plan, DbfRecord record,
            Func<int, object> accessor)
        {
            var buffer = new List<(T Item, object[] Keys)>();
            var position = 0;
            while (ReadNextRow(plan, record, ref position))
            {
                var decision = EvaluateRow(record, plan, accessor);
                if (decision == RowDecision.Stop) break;
                if (decision == RowDecision.Skip) continue;

                buffer.Add((Materialize(record, plan), SnapshotSortKeys(record, plan)));
            }

            return buffer;
        }

        private static bool SortRequired(QueryPlan plan)
        {
            return plan.SortKeys.Count > 0 && !plan.AccessPlan.SortSatisfied;
        }

        private bool NotLimited(int returned)
        {
            return _take == null || returned < _take.Value;
        }

        private enum RowDecision
        {
            Accept,
            Skip,
            Stop
        }

        // the deleted/filter checks parse only the filter's columns; the remaining
        // needed columns parse once the row is accepted. Stop means the record data
        // ran past the end of a stream (matching Read's end-of-stream contract).
        private RowDecision EvaluateRow(DbfRecord record, QueryPlan plan, Func<int, object> accessor)
        {
            if (!MatchesFilter(record, plan, accessor)) return RowDecision.Skip;

            return record.TryParseValues(plan.PostFilterOrdinals) ? RowDecision.Accept : RowDecision.Stop;
        }

        // counting needs only the filter columns, so this is also the whole of the
        // count path's row evaluation
        private bool MatchesFilter(DbfRecord record, QueryPlan plan, Func<int, object> accessor)
        {
            if (!_includeDeleted && record.IsDeleted) return false;
            if (!record.TryParseValues(plan.FilterOrdinals)) return false;

            return plan.Filter == null || plan.Filter.Matches(accessor);
        }

        private static T Materialize(DbfRecord record, QueryPlan plan)
        {
            for (var i = 0; i < plan.MappedOrdinals.Count; i++)
            {
                plan.RowBuffer[i] = record.GetValue(plan.MappedOrdinals[i]);
            }

            return plan.Materializer(plan.RowBuffer);
        }

        private static object[] SnapshotSortKeys(DbfRecord record, QueryPlan plan)
        {
            var keys = new object[plan.SortKeys.Count];
            for (var i = 0; i < keys.Length; i++)
            {
                keys[i] = record.GetValue(plan.SortKeys[i].Ordinal);
            }

            return keys;
        }

        private IEnumerable<T> SortAndLimit(List<(T Item, object[] Keys)> buffer, QueryPlan plan)
        {
            var indices = new int[buffer.Count];
            for (var i = 0; i < indices.Length; i++) indices[i] = i;

            // the original index is the final tiebreaker, making the sort stable
            Array.Sort(indices, (x, y) =>
            {
                var cmp = CompareKeys(buffer[x].Keys, buffer[y].Keys, plan);
                return cmp != 0 ? cmp : x.CompareTo(y);
            });

            var limit = _take ?? buffer.Count;
            for (var i = 0; i < indices.Length && i < limit; i++)
            {
                yield return buffer[indices[i]].Item;
            }
        }

        private static int CompareKeys(object[] x, object[] y, QueryPlan plan)
        {
            for (var i = 0; i < plan.SortKeys.Count; i++)
            {
                var cmp = CompareKeyValues(x[i], y[i]);
                if (plan.SortKeys[i].Descending) cmp = -cmp;
                if (cmp != 0) return cmp;
            }

            return 0;
        }

        // nulls sort before every value ascending (and therefore last descending)
        private static int CompareKeyValues(object x, object y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return -1;
            if (y == null) return 1;

            return SqlValueComparer.CompareValues(x, y, 0);
        }

        private QueryPlan Prepare()
        {
            if (RowMaterializer.IsScalar(typeof(T)))
                throw new InvalidOperationException(
                    $"DbfQuery requires a class with settable properties; '{typeof(T).Name}' is not supported.");

            var columns = _table.Columns;
            var mappedOrdinals = new List<int>();
            var mappedNames = new List<string>();
            var ordinalsByProperty = new Dictionary<string, int>(StringComparer.Ordinal);

            var columnNames = columns.Select(column => column.ColumnName).ToArray();

            foreach (var propertyName in RowMaterializer.GetSettableProperties(typeof(T))
                         .Select(property => property.Name))
            {
                var ordinal = RowMaterializer.FindSourceOrdinal(propertyName, columnNames);
                if (ordinal < 0)
                    throw new InvalidOperationException(
                        $"No column in '{_table.Path}' matches property '{typeof(T).Name}.{propertyName}'.");

                ordinalsByProperty[propertyName] = ordinal;
                mappedOrdinals.Add(ordinal);
                mappedNames.Add(columnNames[ordinal]);
            }

            int ResolveOrdinal(string propertyName)
            {
                if (ordinalsByProperty.TryGetValue(propertyName, out var ordinal)) return ordinal;

                throw new InvalidOperationException(
                    $"Property '{propertyName}' is not mapped to a column.");
            }

            SqlExpression combinedWhere = null;
            foreach (var predicate in _predicates)
            {
                var translated = QueryExpressionTranslator.TranslatePredicate(predicate, ResolveOrdinal);
                combinedWhere = combinedWhere == null
                    ? translated
                    : new SqlBinaryExpression(SqlBinaryOperator.And, combinedWhere, translated);
            }

            var filter = combinedWhere == null ? null : new SqlExpressionEvaluator(combinedWhere, null, null);

            var sortKeys = new List<(int Ordinal, bool Descending)>();
            foreach (var (key, descending) in _orderBy)
            {
                var (ordinal, _) = QueryExpressionTranslator.TranslateSortKey(key, ResolveOrdinal);
                sortKeys.Add((ordinal, descending));
            }

            var accessPlan = QueryPlanner.CreatePlan(combinedWhere, sortKeys, _table, _useIndexes, null, null);

            // column-subset parsing (issue #296): the filter's columns are parsed for
            // every candidate row; the remaining mapped (and, when sorting, sort key)
            // columns only for rows that match
            var filterOrdinals = combinedWhere == null
                ? new HashSet<int>()
                : SqlColumnCollector.CollectOrdinals(combinedWhere);

            var needed = new HashSet<int>(mappedOrdinals);
            if (sortKeys.Count > 0 && !accessPlan.SortSatisfied)
            {
                foreach (var (ordinal, _) in sortKeys)
                {
                    needed.Add(ordinal);
                }
            }

            needed.ExceptWith(filterOrdinals);

            return new QueryPlan(
                RowMaterializer.Create<T>(mappedNames),
                mappedOrdinals,
                new object[mappedOrdinals.Count],
                filter,
                sortKeys,
                accessPlan)
            {
                FilterOrdinals = SqlColumnCollector.ToSortedOrdinals(filterOrdinals),
                PostFilterOrdinals = SqlColumnCollector.ToSortedOrdinals(needed)
            };
        }

        private sealed class QueryPlan
        {
            public QueryPlan(Func<object[], T> materializer, IReadOnlyList<int> mappedOrdinals, object[] rowBuffer,
                SqlExpressionEvaluator filter, IReadOnlyList<(int Ordinal, bool Descending)> sortKeys,
                QueryAccessPlan accessPlan)
            {
                Materializer = materializer;
                MappedOrdinals = mappedOrdinals;
                RowBuffer = rowBuffer;
                Filter = filter;
                SortKeys = sortKeys;
                AccessPlan = accessPlan;
            }

            public Func<object[], T> Materializer { get; }

            public IReadOnlyList<int> MappedOrdinals { get; }

            public object[] RowBuffer { get; }

            public SqlExpressionEvaluator Filter { get; }

            public IReadOnlyList<(int Ordinal, bool Descending)> SortKeys { get; }

            public QueryAccessPlan AccessPlan { get; }

            public int[] FilterOrdinals { get; set; }

            public int[] PostFilterOrdinals { get; set; }
        }
    }
}
