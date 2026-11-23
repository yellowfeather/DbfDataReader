using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DbfDataReader.Query;

namespace DbfDataReader
{
    public class DbfDbConnection : DbConnection
    {
        private readonly Dictionary<Type, ModelMap> _modelMaps = new Dictionary<Type, ModelMap>();
        private string _database = string.Empty;
        private ConnectionState _state  = ConnectionState.Closed;

        public DbfDbConnection()
        {
            DataSource = string.Empty;
            ServerVersion = string.Empty;
        }

        public DbfDbConnection(string dataSource, string serverVersion)
        {
            DataSource = dataSource;
            ServerVersion = serverVersion;
        }

        public DbfDataReaderOptions Options { get; private set; } = new DbfDataReaderOptions();

        public override string ConnectionString { get; set; }
        public override string DataSource { get; }
        public override string ServerVersion { get; }
        
        public override ConnectionState State => _state;
        public override string Database => _database;
        
        public override void ChangeDatabase(string databaseName)
        {
            if (!Directory.Exists(databaseName))
            {
                throw new DirectoryNotFoundException(databaseName);
            }
            
            _database = databaseName;
        }

        public override void Close()
        {
            _state = ConnectionState.Closed;
        }

        public override void Open()
        {
            var builder = new DbfDbConnectionStringBuilder(ConnectionString);

            ChangeDatabase(builder.Folder);

            var options = new DbfDataReaderOptions();
            
            if (builder.Encoding is { } encoding) 
            {
                options.Encoding = Encoding.GetEncoding(encoding);
            }

            if (builder.ReadFloatsAsDecimals is var readFloatsAsDecimals)
            {
                options.ReadFloatsAsDecimals = readFloatsAsDecimals;
            }

            if (builder.SkipDeletedRecords is var skipDeletedRecords) 
            {
                options.SkipDeletedRecords = skipDeletedRecords;
            }

            if (builder.StringTrimming is var stringTrimming)
            {
                options.StringTrimming = stringTrimming;
            }

            if (builder.UseIndexes is var useIndexes)
            {
                options.UseIndexes = useIndexes;
            }

            Options = options;
            _state = ConnectionState.Open;
        }

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel)
        {
            throw new NotImplementedException();
        }

        protected override DbCommand CreateDbCommand()
        {
            return new DbfDbCommand
            {
                Connection = this,
            };
        }

        // Registers how T's properties map to columns for the typed queries on this
        // connection, e.g.
        //
        //     connection.Map<Row>(map => map.Property(r => r.Coverage).HasColumnName("Coverage %"));
        //
        // The mapping takes precedence over [Column] / [NotMapped]. Registering T again
        // replaces its mapping for subsequent queries.
        public DbfDbConnection Map<T>(Action<DbfModelBuilder<T>> configure) where T : class
        {
            if (configure == null) throw new ArgumentNullException(nameof(configure));

            if (RowMaterializer.IsScalar(typeof(T)))
                throw new InvalidOperationException(
                    $"A column mapping cannot be configured for the scalar type '{typeof(T).Name}'.");

            _modelMaps[typeof(T)] = DbfModelBuilder<T>.BuildMap(configure);
            return this;
        }

        // Dapper-style typed queries: rows are mapped to T's settable properties by
        // column name (or to T itself for single-column scalar queries), and param's
        // properties become named parameters. Map<T> changes the column names.
        public List<T> Query<T>(string sql, object param = null)
        {
            using (var command = CreateTypedCommand(sql, param))
            using (var reader = command.ExecuteReader())
            {
                var materializer = CreateMaterializer<T>(reader);
                var row = new object[reader.FieldCount];

                var results = new List<T>();
                while (reader.Read())
                {
                    reader.GetValues(row);
                    results.Add(materializer(row));
                }

                return results;
            }
        }

        public async Task<List<T>> QueryAsync<T>(string sql, object param = null,
            CancellationToken cancellationToken = default)
        {
            using (var command = CreateTypedCommand(sql, param))
            using (var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false))
            {
                var materializer = CreateMaterializer<T>(reader);
                var row = new object[reader.FieldCount];

                var results = new List<T>();
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    reader.GetValues(row);
                    results.Add(materializer(row));
                }

                return results;
            }
        }

        public T QueryFirstOrDefault<T>(string sql, object param = null)
        {
            using (var command = CreateTypedCommand(sql, param))
            using (var reader = command.ExecuteReader())
            {
                if (!reader.Read()) return default;

                var materializer = CreateMaterializer<T>(reader);
                var row = new object[reader.FieldCount];
                reader.GetValues(row);

                return materializer(row);
            }
        }

        private DbfDbCommand CreateTypedCommand(string sql, object param)
        {
            var command = (DbfDbCommand)CreateCommand();
            command.CommandText = sql;

            if (param != null)
            {
                foreach (var property in param.GetType().GetProperties())
                {
                    command.Parameters.AddWithValue(property.Name, property.GetValue(param));
                }
            }

            return command;
        }

        private Func<object[], T> CreateMaterializer<T>(DbDataReader reader)
        {
            var names = new string[reader.FieldCount];
            for (var ordinal = 0; ordinal < names.Length; ordinal++)
            {
                names[ordinal] = reader.GetName(ordinal);
            }

            // null (no registration) selects the attribute / convention mapping
            _modelMaps.TryGetValue(typeof(T), out var map);
            return RowMaterializer.Create<T>(names, map);
        }
    }
}
