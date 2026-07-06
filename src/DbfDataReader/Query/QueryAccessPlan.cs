using System.Collections.Generic;

namespace DbfDataReader.Query
{
    // how a query fetches its rows: sequentially, or by seeking a pre-computed list of
    // record indexes obtained from an index search
    internal sealed class QueryAccessPlan
    {
        private QueryAccessPlan(IReadOnlyList<int> recordIndexes, bool sortSatisfied, string description)
        {
            RecordIndexes = recordIndexes;
            SortSatisfied = sortSatisfied;
            Description = description;
        }

        // null for a sequential full scan
        public IReadOnlyList<int> RecordIndexes { get; }

        // true when the rows arrive already in ORDER BY order
        public bool SortSatisfied { get; }

        public string Description { get; }

        public static QueryAccessPlan FullScan(string reason)
        {
            return new QueryAccessPlan(null, false, $"full scan ({reason})");
        }

        public static QueryAccessPlan Index(IReadOnlyList<int> recordIndexes, bool sortSatisfied, string description)
        {
            return new QueryAccessPlan(recordIndexes, sortSatisfied, description);
        }
    }
}
