using System;
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
        protected override DbParameterCollection DbParameterCollection { get; }
        protected override DbTransaction DbTransaction { get; set; }

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
            throw new NotImplementedException();
        }

        public override void Prepare()
        {
            throw new NotImplementedException();
        }

        protected override DbParameter CreateDbParameter()
        {
            throw new NotImplementedException();
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
            EnsureSupported(statement);

            var filePath = GetFilePath(folder, statement.TableName);

            var options = dbfDbConnection.Options;
            var reader = new DbfDataReader(filePath, options);

            // a plain SELECT * needs no projection or row limit; return the raw reader
            if (statement.IsSelectAll && statement.Top == null) return reader;

            try
            {
                SqlBinder.Bind(statement, reader.DbfTable.Columns);
                return new DbfQueryDataReader(reader, statement);
            }
            catch
            {
                reader.Dispose();
                throw;
            }
        }

        // execution catches up with the parser in later phases; parse everything,
        // run what is implemented
        private static void EnsureSupported(SelectStatement statement)
        {
            if (statement.Where != null)
                throw new NotSupportedException("WHERE clauses are not supported yet.");
            if (statement.OrderBy.Count > 0)
                throw new NotSupportedException("ORDER BY is not supported yet.");
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
