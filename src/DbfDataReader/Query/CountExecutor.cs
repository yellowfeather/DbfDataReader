using System;
using System.Collections.Generic;

namespace DbfDataReader.Query
{
    // Executes SELECT COUNT(*) without materializing rows, picking the cheapest safe
    // strategy: when an index search covers the WHERE clause exactly, the count is the
    // size of the search result (with per-record status checks only when deleted rows
    // must be skipped, since index entries include them); without a WHERE clause,
    // records are scanned reading status bytes only, skipping value parsing entirely
    // (the header record count is not trusted - real-world files get it wrong); in all
    // other cases the rows are read and filtered without being projected.
    internal static class CountExecutor
    {
        public const string StatusScanDescription = "count via record status scan";

        public static string DescribeStrategy(QueryAccessPlan plan, bool skipDeletedRecords)
        {
            if (plan.RecordIndexes != null && plan.CoversWhereExactly)
            {
                return plan.Description +
                       (skipDeletedRecords ? " (count with record status checks)" : " (count from index only)");
            }

            return plan.Description + " (count by reading rows)";
        }

        public static (int Count, string Description) Execute(SelectStatement statement, DbfDataReader reader,
            DbfDataReaderOptions options, IReadOnlyDictionary<string, object> namedParameters,
            IReadOnlyList<object> positionalParameters)
        {
            var table = reader.DbfTable;
            var record = reader.DbfRecord;

            if (statement.Where == null)
                return (CountByStatusScan(table, record, options.SkipDeletedRecords), StatusScanDescription);

            var plan = QueryPlanner.CreatePlan(statement.Where,
                Array.Empty<(int Ordinal, bool Descending)>(), table, options.UseIndexes, namedParameters,
                positionalParameters);
            var description = DescribeStrategy(plan, options.SkipDeletedRecords);

            if (plan.RecordIndexes != null && plan.CoversWhereExactly)
            {
                if (!options.SkipDeletedRecords) return (plan.RecordIndexes.Count, description);

                return (CountByStatusChecks(table, record, plan.RecordIndexes), description);
            }

            var evaluator = new SqlExpressionEvaluator(statement.Where, namedParameters, positionalParameters);
            var filterOrdinals =
                SqlColumnCollector.ToSortedOrdinals(SqlColumnCollector.CollectOrdinals(statement.Where));
            record.EnableSubsetParsing();
            return (CountByReadingRows(reader, evaluator, plan, filterOrdinals), description);
        }

        private static int CountByStatusScan(DbfTable table, DbfRecord record, bool skipDeletedRecords)
        {
            var count = 0;
            while (table.ReadRaw(record))
            {
                if (skipDeletedRecords && record.IsDeleted) continue;

                count++;
            }

            return count;
        }

        private static int CountByStatusChecks(DbfTable table, DbfRecord record, IReadOnlyList<int> recordIndexes)
        {
            var count = 0;
            foreach (var recordIndex in recordIndexes)
            {
                table.Seek(recordIndex);
                if (!table.ReadRaw(record)) continue; // entry beyond the table
                if (record.IsDeleted) continue;

                count++;
            }

            return count;
        }

        // rows are counted parsing only the columns the WHERE clause references
        private static int CountByReadingRows(DbfDataReader reader, SqlExpressionEvaluator evaluator,
            QueryAccessPlan plan, int[] filterOrdinals)
        {
            Func<int, object> accessor = reader.GetValue;
            var table = reader.DbfTable;
            var record = reader.DbfRecord;

            var count = 0;

            if (plan.RecordIndexes == null)
            {
                // reader.ReadRaw applies the skip-deleted option itself
                while (reader.ReadRaw())
                {
                    if (!record.TryParseValues(filterOrdinals)) break;
                    if (evaluator.Matches(accessor)) count++;
                }

                return count;
            }

            foreach (var recordIndex in plan.RecordIndexes)
            {
                table.Seek(recordIndex);
                if (!table.ReadRaw(record)) continue;
                if (reader.SkipsDeletedRecords && record.IsDeleted) continue;
                if (!record.TryParseValues(filterOrdinals)) break;
                if (!evaluator.Matches(accessor)) continue;

                count++;
            }

            return count;
        }
    }
}
