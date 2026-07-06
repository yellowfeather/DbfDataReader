using System.Collections.Generic;

namespace DbfDataReader.Query
{
    // how a query fetches its rows: sequentially, or by seeking a pre-computed list of
    // record indexes obtained from an index search
    internal sealed class QueryAccessPlan
    {
        private QueryAccessPlan(IReadOnlyList<int> recordIndexes, bool sortSatisfied, bool coversWhereExactly,
            string description)
        {
            RecordIndexes = recordIndexes;
            SortSatisfied = sortSatisfied;
            CoversWhereExactly = coversWhereExactly;
            Description = description;
        }

        // null for a sequential full scan
        public IReadOnlyList<int> RecordIndexes { get; }

        // true when the rows arrive already in ORDER BY order
        public bool SortSatisfied { get; }

        // true when the record indexes are exactly the rows matching the WHERE clause,
        // making the residual filter redundant (deleted-row handling still applies)
        public bool CoversWhereExactly { get; }

        public string Description { get; }

        public static QueryAccessPlan FullScan(string reason)
        {
            return new QueryAccessPlan(null, false, false, $"full scan ({reason})");
        }

        public static QueryAccessPlan Index(IReadOnlyList<int> recordIndexes, bool sortSatisfied,
            bool coversWhereExactly, string description)
        {
            return new QueryAccessPlan(recordIndexes, sortSatisfied, coversWhereExactly, description);
        }
    }
}
