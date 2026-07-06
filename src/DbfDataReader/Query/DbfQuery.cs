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

        // a sidecar .cdx index is used automatically when it can serve the query;
        // this forces a sequential scan instead
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
            Func<int, object> accessor = record.GetValue;

            _table.Seek(0);

            var position = 0;
            var count = 0;
            while (ReadNextRow(plan, record, ref position))
            {
                if (!Accept(record, plan, accessor)) continue;

                count++;
                if (_take != null && count >= _take.Value) break;
            }

            return count;
        }

        // advances to the next candidate row: sequentially, or by seeking the next
        // record index supplied by the access plan
        private bool ReadNextRow(QueryPlan plan, DbfRecord record, ref int position)
        {
            var recordIndexes = plan.AccessPlan.RecordIndexes;
            if (recordIndexes == null) return _table.Read(record);

            while (position < recordIndexes.Count)
            {
                _table.Seek(recordIndexes[position]);
                position++;
                if (_table.Read(record)) return true; // false: entry beyond the table
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
            Func<int, object> accessor = record.GetValue;
            var recordIndexes = plan.AccessPlan.RecordIndexes;
            var position = 0;

            async Task<bool> MoveNextAsync()
            {
                if (recordIndexes == null)
                    return await _table.ReadAsync(record, cancellationToken).ConfigureAwait(false);

                while (position < recordIndexes.Count)
                {
                    _table.Seek(recordIndexes[position]);
                    position++;
                    if (await _table.ReadAsync(record, cancellationToken).ConfigureAwait(false)) return true;
                }

                return false;
            }

            _table.Seek(0);

            if (!SortRequired(plan))
            {
                var returned = 0;
                while (NotLimited(returned) && await MoveNextAsync().ConfigureAwait(false))
                {
                    if (!Accept(record, plan, accessor)) continue;

                    yield return Materialize(record, plan);
                    returned++;
                }

                yield break;
            }

            var buffer = new List<(T Item, object[] Keys)>();
            while (await MoveNextAsync().ConfigureAwait(false))
            {
                if (!Accept(record, plan, accessor)) continue;

                buffer.Add((Materialize(record, plan), SnapshotSortKeys(record, plan)));
            }

            foreach (var item in SortAndLimit(buffer, plan))
            {
                yield return item;
            }
        }

        private IEnumerable<T> Execute()
        {
            var plan = Prepare();
            var record = new DbfRecord(_table);
            Func<int, object> accessor = record.GetValue;

            _table.Seek(0);

            var position = 0;
            if (!SortRequired(plan))
            {
                var returned = 0;
                while (NotLimited(returned) && ReadNextRow(plan, record, ref position))
                {
                    if (!Accept(record, plan, accessor)) continue;

                    yield return Materialize(record, plan);
                    returned++;
                }

                yield break;
            }

            var buffer = new List<(T Item, object[] Keys)>();
            while (ReadNextRow(plan, record, ref position))
            {
                if (!Accept(record, plan, accessor)) continue;

                buffer.Add((Materialize(record, plan), SnapshotSortKeys(record, plan)));
            }

            foreach (var item in SortAndLimit(buffer, plan))
            {
                yield return item;
            }
        }

        private static bool SortRequired(QueryPlan plan)
        {
            return plan.SortKeys.Count > 0 && !plan.AccessPlan.SortSatisfied;
        }

        private bool NotLimited(int returned)
        {
            return _take == null || returned < _take.Value;
        }

        private bool Accept(DbfRecord record, QueryPlan plan, Func<int, object> accessor)
        {
            if (!_includeDeleted && record.IsDeleted) return false;

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

            return new QueryPlan(
                RowMaterializer.Create<T>(mappedNames),
                mappedOrdinals,
                new object[mappedOrdinals.Count],
                filter,
                sortKeys,
                accessPlan);
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
        }
    }
}
