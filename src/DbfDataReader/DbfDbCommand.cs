using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Linq;
using DbfDataReader.Query;

namespace DbfDataReader
{
    public class DbfDbCommand : DbCommand
    {
        public override string CommandText { get; set; }
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection DbConnection { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new DbfDbParameterCollection();
        protected override DbTransaction DbTransaction { get; set; }

        public new DbfDbParameterCollection Parameters => (DbfDbParameterCollection)DbParameterCollection;

        public override void Cancel()
        {
            throw new NotImplementedException();
        }

        public override int ExecuteNonQuery()
        {
            throw new NotImplementedException();
        }

        public override object ExecuteScalar()
        {
            using (var reader = ExecuteDbDataReader(CommandBehavior.Default))
            {
                return reader.Read() ? reader.GetValue(0) : null;
            }
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        protected override DbParameter CreateDbParameter()
        {
            return new DbfDbParameter();
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            var dbfDbConnection = Connection as DbfDbConnection;
            if (dbfDbConnection is null)
            {
                throw new InvalidOperationException($"{nameof(DbfDbConnection)} is not available");
            }

            var folder = dbfDbConnection.Database;
            if (string.IsNullOrWhiteSpace(folder))
            {
                throw new DirectoryNotFoundException("No folder was specified for the Dbf files.");
            }
            
            if (!Directory.Exists(folder))
            {
                throw new DirectoryNotFoundException($"The specified folder does not exist: {folder}");
            }
            
            var statement = SqlParser.Parse(CommandText);

            var filePath = GetFilePath(folder, statement.TableName);

            var options = dbfDbConnection.Options;

            if (statement.IsCountAll) return ExecuteCount(statement, filePath, options);

            var reader = new DbfDataReader(filePath, options);

            // a plain unfiltered select-all needs no projection, filter, ordering or row
            // limit, so it stays on the raw reader
            if (statement.IsSelectAll && statement.Top == null && statement.Where == null &&
                statement.OrderBy.Count == 0)
                return reader;

            try
            {
                SqlBinder.Bind(statement, reader.DbfTable.Columns);

                var (namedParameters, positionalParameters) = CollectParameters();
                var evaluator = statement.Where == null
                    ? null
                    : new SqlExpressionEvaluator(statement.Where, namedParameters, positionalParameters);
                var plan = CreatePlan(statement, reader.DbfTable, options, namedParameters, positionalParameters);

                return new DbfQueryDataReader(reader, statement, evaluator, plan, options.SkipDeletedRecords);
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        // describes how the current command text would be executed, without reading any
        // rows; useful for verifying whether an index is used
        public string ExplainPlan()
        {
            var dbfDbConnection = Connection as DbfDbConnection;
            if (dbfDbConnection is null)
            {
                throw new InvalidOperationException($"{nameof(DbfDbConnection)} is not available");
            }

            var statement = SqlParser.Parse(CommandText);
            var filePath = GetFilePath(dbfDbConnection.Database, statement.TableName);
            var options = dbfDbConnection.Options;

            using (var table = new DbfTable(filePath, options.Encoding, options.StringTrimming,
                       options.ReadFloatsAsDecimals))
            {
                if (statement.IsCountAll)
                {
                    SqlBinder.Bind(statement, table.Columns);
                    if (statement.Where == null) return CountExecutor.StatusScanDescription;

                    var (namedCount, positionalCount) = CollectParameters();
                    var countPlan = QueryPlanner.CreatePlan(statement.Where,
                        Array.Empty<(int Ordinal, bool Descending)>(), table, options.UseIndexes, namedCount,
                        positionalCount);
                    return CountExecutor.DescribeStrategy(countPlan, options.SkipDeletedRecords);
                }

                if (statement.IsSelectAll && statement.Top == null && statement.Where == null &&
                    statement.OrderBy.Count == 0)
                    return "full scan (select all)";

                SqlBinder.Bind(statement, table.Columns);

                var (namedParameters, positionalParameters) = CollectParameters();
                var plan = CreatePlan(statement, table, options, namedParameters, positionalParameters);

                var sortNote = statement.OrderBy.Count > 0 && !plan.SortSatisfied ? "; in-memory sort" : "";
                return plan.Description + sortNote;
            }
        }

        private DbDataReader ExecuteCount(SelectStatement statement, string filePath, DbfDataReaderOptions options)
        {
            using (var reader = new DbfDataReader(filePath, options))
            {
                SqlBinder.Bind(statement, reader.DbfTable.Columns);

                var (namedParameters, positionalParameters) = CollectParameters();
                var (count, _) = CountExecutor.Execute(statement, reader, options, namedParameters,
                    positionalParameters);

                return new ScalarDataReader("count", count, statement.Top == 0 ? 0 : 1);
            }
        }

        private static QueryAccessPlan CreatePlan(SelectStatement statement, DbfTable table,
            DbfDataReaderOptions options, IReadOnlyDictionary<string, object> namedParameters,
            IReadOnlyList<object> positionalParameters)
        {
            var orderKeys = statement.OrderBy.Select(item => (item.Ordinal, item.Descending)).ToList();
            return QueryPlanner.CreatePlan(statement.Where, orderKeys, table, options.UseIndexes, namedParameters,
                positionalParameters);
        }

        private (Dictionary<string, object> Named, List<object> Positional) CollectParameters()
        {
            var namedParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var positionalParameters = new List<object>();

            foreach (DbfDbParameter parameter in Parameters)
            {
                positionalParameters.Add(parameter.Value);

                var name = DbfDbParameterCollection.Normalize(parameter.ParameterName);
                if (!string.IsNullOrEmpty(name)) namedParameters[name] = parameter.Value;
            }

            return (namedParameters, positionalParameters);
        }

        private static string GetFilePath(string folder, string fileName)
        {
            var filePath = Path.Combine(folder, $"{fileName}");
            if (File.Exists(filePath))
            {
                return filePath;
            }
            
            filePath = Path.ChangeExtension(filePath, ".dbf");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            filePath = Path.ChangeExtension(filePath, ".DBF");
            if (File.Exists(filePath))
            {
                return filePath;
            }

            throw new FileNotFoundException(filePath);
        }
    }
}
