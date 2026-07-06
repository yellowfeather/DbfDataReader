using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
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
            var reader = new DbfDataReader(filePath, options);

            // a plain unfiltered select-all needs no projection, filter, ordering or row
            // limit, so it stays on the raw reader
            if (statement.IsSelectAll && statement.Top == null && statement.Where == null &&
                statement.OrderBy.Count == 0)
                return reader;

            try
            {
                SqlBinder.Bind(statement, reader.DbfTable.Columns);
                return new DbfQueryDataReader(reader, statement, CreateEvaluator(statement));
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        private SqlExpressionEvaluator CreateEvaluator(SelectStatement statement)
        {
            if (statement.Where == null) return null;

            var namedParameters = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            var positionalParameters = new List<object>();

            foreach (DbfDbParameter parameter in Parameters)
            {
                positionalParameters.Add(parameter.Value);

                var name = DbfDbParameterCollection.Normalize(parameter.ParameterName);
                if (!string.IsNullOrEmpty(name)) namedParameters[name] = parameter.Value;
            }

            return new SqlExpressionEvaluator(statement.Where, namedParameters, positionalParameters);
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
