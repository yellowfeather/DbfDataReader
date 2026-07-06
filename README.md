# DbfDataReader

[![CI](https://github.com/yellowfeather/DbfDataReader/actions/workflows/ci.yml/badge.svg)](https://github.com/yellowfeather/DbfDataReader/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/dt/DbfDataReader.svg)](https://www.nuget.org/packages/DbfDataReader)
[![NuGet](https://img.shields.io/nuget/vpre/DbfDataReader.svg)](https://www.nuget.org/packages/DbfDataReader)
[![MyGet Build Status](https://www.myget.org/BuildSource/Badge/dbfdatareader?identifier=54ae0096-55d5-418c-8eb9-54a35df720fb)](https://www.myget.org/)

DbfDataReader is a small, fast .NET library for reading dBase, xBase, Clipper and FoxPro
database files (`.dbf`), with support for memo files, Visual FoxPro compound indexes
(`.cdx`), a `DbDataReader` implementation for easy integration (e.g. `SqlBulkCopy`), an
ADO.NET provider with a SQL `SELECT` dialect, and typed queries in the style of LINQ and
Dapper.

## Table of contents

- [Installation](#installation)
- [Reading a DBF file](#reading-a-dbf-file)
  - [Table and column information](#table-and-column-information)
  - [Iterating over rows](#iterating-over-rows)
- [DbDataReader implementation](#dbdatareader-implementation)
  - [Bulk copy to SQL Server](#bulk-copy-to-sql-server)
- [Async reading](#async-reading)
- [Random access with Seek](#random-access-with-seek)
- [Compound index (.cdx) support](#compound-index-cdx-support)
- [Typed queries with Query&lt;T&gt;](#typed-queries-with-queryt)
- [SQL queries with DbfDbConnection](#sql-queries-with-dbfdbconnection)
  - [Supported SQL](#supported-sql)
  - [Dapper-style typed queries](#dapper-style-typed-queries)
  - [Automatic index usage](#automatic-index-usage)
  - [Connection string options](#connection-string-options)
- [Used by](#used-by)
- [License](#license)

## Installation

Install the [NuGet package](https://www.nuget.org/packages/DbfDataReader):

```
dotnet add package DbfDataReader
```

The library targets `net10.0` and `netstandard2.1`.

## Reading a DBF file

### Table and column information

Open a table with `DbfTable` to inspect the header and columns:

```csharp
var dbfPath = "path/file.dbf";
using (var dbfTable = new DbfTable(dbfPath, Encoding.UTF8))
{
    var header = dbfTable.Header;

    var versionDescription = header.VersionDescription;
    var hasMemo = dbfTable.Memo != null;
    var recordCount = header.RecordCount;

    foreach (var dbfColumn in dbfTable.Columns)
    {
        var name = dbfColumn.ColumnName;
        var columnType = dbfColumn.ColumnType;
        var length = dbfColumn.Length;
        var decimalCount = dbfColumn.DecimalCount;
    }
}
```

### Iterating over rows

```csharp
var skipDeleted = true;

var dbfPath = "path/file.dbf";
using (var dbfTable = new DbfTable(dbfPath, Encoding.UTF8))
{
    var dbfRecord = new DbfRecord(dbfTable);

    while (dbfTable.Read(dbfRecord))
    {
        if (skipDeleted && dbfRecord.IsDeleted)
        {
            continue;
        }

        foreach (var dbfValue in dbfRecord.Values)
        {
            var stringValue = dbfValue.ToString();
            var obj = dbfValue.GetValue();
        }
    }
}
```

## DbDataReader implementation

There is also an implementation of `DbDataReader`:

```csharp
var options = new DbfDataReaderOptions
{
    SkipDeletedRecords = true
    // Encoding = EncodingProvider.GetEncoding(1252);
};

var dbfPath = "path/file.dbf";
using (var dbfDataReader = new DbfDataReader(dbfPath, options))
{
    while (dbfDataReader.Read())
    {
        var valueCol1 = dbfDataReader.GetString(0);
        var valueCol2 = dbfDataReader.GetDecimal(1);
        var valueCol3 = dbfDataReader.GetDateTime(2);
        var valueCol4 = dbfDataReader.GetInt32(3);
    }
}
```

### Bulk copy to SQL Server

Because `DbfDataReader` is a `DbDataReader`, you can bulk copy to MS SQL Server:

```csharp
var options = new DbfDataReaderOptions
{
    SkipDeletedRecords = true
    // Encoding = EncodingProvider.GetEncoding(1252);
};

var dbfPath = "path/file.dbf";
using (var dbfDataReader = new DbfDataReader(dbfPath, options))
{
    using (var bulkCopy = new SqlBulkCopy(connection))
    {
        bulkCopy.DestinationTableName = "DestinationTableName";

        try
        {
            bulkCopy.WriteToServer(dbfDataReader);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error importing: dbf file: '{dbfPath}', exception: {ex.Message}");
        }
    }
}
```

## Async reading

Records can also be read asynchronously — `DbfDataReader` overrides `ReadAsync`, and
`DbfTable` has `ReadAsync`/`ReadRecordAsync` counterparts. Each record is fetched with a
single buffered asynchronous read and parsed in memory:

```csharp
var dbfPath = "path/file.dbf";
using (var dbfDataReader = new DbfDataReader(dbfPath))
{
    while (await dbfDataReader.ReadAsync(cancellationToken))
    {
        var valueCol1 = dbfDataReader.GetString(0);
    }
}
```

## Random access with Seek

Records can be accessed randomly by zero-based index using `Seek`, available on both
`DbfTable` and `DbfDataReader`. The index of the record most recently read is available
as `RecordIndex`:

```csharp
var dbfPath = "path/file.dbf";
using (var dbfDataReader = new DbfDataReader(dbfPath))
{
    dbfDataReader.Seek(41); // position at the 42nd record

    if (dbfDataReader.Read())
    {
        var recordIndex = dbfDataReader.RecordIndex; // 41
        var valueCol1 = dbfDataReader.GetString(0);
    }
}
```

## Compound index (.cdx) support

Visual FoxPro compound index files (`.cdx`) can be opened and searched, and search results
combined with `Seek` to jump straight to the matching records — `CdxKeyEntry.RecordIndex`
converts the index's one-based record numbers to the zero-based indexes `Seek` expects:

```csharp
using DbfDataReader.Cdx;

var dbfPath = "path/file.dbf";
var cdxPath = "path/file.cdx";

using (var dbfTable = new DbfTable(dbfPath))
using (var cdxFile = new CdxFile(cdxPath, dbfTable.CurrentEncoding))
{
    var tagNames = cdxFile.TagNames;             // the named indexes ("tags") in the file

    var index = cdxFile.GetIndex("CONTACT_ID");  // one tag; index.KeyExpression describes the key
    var dbfRecord = new DbfRecord(dbfTable);

    foreach (var entry in index.Search("C0000000042"))
    {
        dbfTable.Seek(entry.RecordIndex);
        dbfTable.Read(dbfRecord);
        // dbfRecord now holds the matching row
    }
}
```

`CdxIndex` also supports `EnumerateEntries()` (full in-order scan), `Count()`, and a
`Search(Func<byte[], int>)` overload for range or prefix searches.

Current limitations: only ascending indexes with byte-wise (MACHINE collation) character
keys are searchable, index key expressions are exposed as text but not evaluated, and
index entries include deleted records (check `DbfRecord.IsDeleted` after seeking).

## Typed queries with Query&lt;T&gt;

Rows can be mapped straight to your own types. `DbfTable.Query<T>()` is a typed query
builder whose `Where`/`OrderBy` lambdas are translated into the same query engine — the
type's public settable properties define which columns are read, matched by name
(exact first, then case-insensitively):

```csharp
public class GpsPoint
{
    public string Point_ID { get; set; }
    public decimal? Max_PDOP { get; set; }
    public DateTime? Date_Visit { get; set; }
}

using (var dbfTable = new DbfTable("path/dbase_03.dbf"))
{
    var points = dbfTable.Query<GpsPoint>()
        .Where(p => p.Max_PDOP >= 3.5m && p.Point_ID.StartsWith("A"))
        .OrderByDescending(p => p.Date_Visit)
        .Take(10)
        .ToList(); // also foreach, First/FirstOrDefault, Count, ToListAsync, AsAsyncEnumerable
}
```

Supported inside `Where`: comparisons, `&&`/`||`/`!`, `== null`/`!= null` (translated to
`IS [NOT] NULL`), `string.StartsWith`/`EndsWith`/`Contains` (translated to `LIKE`), and
`collection.Contains(p.Column)` (translated to `IN`). Deleted records are skipped unless
`.IncludeDeleted()` is called. Anything that cannot be translated throws
`NotSupportedException` — nothing falls back to silent in-memory evaluation.

## SQL queries with DbfDbConnection

There is also an implementation of `DbConnection` so you can query a folder of files:

```csharp
var dbConnection = new DbfDbConnection(string.Empty, string.Empty);
dbConnection.ConnectionString = $"Folder=./test/fixtures;SkipDeletedRecords=false";
dbConnection.Open();

var dbCommand = dbConnection.CreateCommand();
dbCommand.CommandText = "select * from dbase_03.dbf;";

var reader = await dbCommand.ExecuteReaderAsync();
while (await reader.ReadAsync())
{
    var valueCol1 = reader.GetString(0);
    var valueCol11 = reader.GetDecimal(10);
}
```

### Supported SQL

The command text supports column lists with optional aliases, a row limit, `WHERE`
clauses with named (`@name`) or positional (`?`) parameters, and `ORDER BY`:

```sql
select top 10 Point_ID as id, Date_Visit from dbase_03.dbf
select Point_ID, Max_PDOP from dbase_03.dbf limit 5
select Point_ID from dbase_03.dbf where Max_PDOP >= 3.5 and Date_Visit >= '1997-01-01'
select Point_ID from dbase_03.dbf where Point_ID like 'A%' or Point_ID in ('B1', 'B2')
select * from dbase_03.dbf where Point_ID = @id
select top 5 Point_ID from dbase_03.dbf where Max_PDOP >= 3.5 order by Max_PDOP desc, Point_ID
select count(*) from dbase_03.dbf where Max_PDOP >= 3.5
```

```csharp
var command = (DbfDbCommand)dbConnection.CreateCommand();
command.CommandText = "select Point_ID from dbase_03.dbf where Point_ID = @id";
command.Parameters.AddWithValue("@id", "A1");
var value = command.ExecuteScalar();
```

Predicates support `=`, `<>`, `!=`, `<`, `<=`, `>`, `>=`, `BETWEEN`, `IN`, `LIKE`
(`%` and `_`), `IS [NOT] NULL`, `AND`/`OR`/`NOT` and parentheses. String comparisons are
ordinal, case-sensitive and ignore trailing spaces; comparisons involving `NULL` follow
SQL three-valued logic; date columns compare against `'yyyy-MM-dd'` or
`'yyyy-MM-dd HH:mm:ss'` strings. `ORDER BY` sorts with the same comparison rules
(multiple keys, `ASC`/`DESC`, select-list aliases allowed, nulls first ascending) using
a stable in-memory sort of the matching rows; `TOP`/`LIMIT` applies after the sort.
`COUNT(*)` is the one supported aggregate, and it counts as cheaply as it safely can:
without a `WHERE` it scans record status bytes only (header record counts are not
trusted); when an index covers the `WHERE` exactly, the count comes from the index
without reading any rows; otherwise rows are read and filtered without being projected.
The same fast paths back `DbfQuery<T>.Count()`.

### Dapper-style typed queries

`DbfDbConnection` also offers the same row-to-type mapping over SQL text in the style of
Dapper — and the provider is Dapper-compatible if you prefer the real thing:

```csharp
var points = dbConnection.Query<GpsPoint>(
    "select Point_ID, Max_PDOP, Date_Visit from dbase_03.dbf where Point_ID = @id",
    new { id = "A1" });
var ids = dbConnection.Query<string>("select Point_ID from dbase_03.dbf"); // scalar rows
// also QueryAsync<T> and QueryFirstOrDefault<T>
```

### Automatic index usage

When a sidecar compound index (`file.cdx`) exists next to the table, queries use it
automatically: equality, range and `BETWEEN` predicates on indexed character, integer,
numeric, double and date columns (plus prefix `LIKE` on character columns) become index
seeks, and an `ORDER BY` matching an index tag — ascending or descending — reads in
index order instead of sorting (descending order is served by reversing the ascending
tag). This applies to SQL text and to the `Query<T>` builder alike.

The planner is conservative — index tags with dBASE `UNIQUE` or `FOR` filters,
descending keys, expression keys, unsupported key types (datetime, currency), or
non-ASCII character search values fall back to a full table scan, and the full `WHERE`
clause is always re-applied to every row an index returns.

Set `UseIndexes=false` in the connection string (or call `.WithoutIndexes()` on the
builder) to force scans, and use `DbfDbCommand.ExplainPlan()` or
`DbfQuery<T>.ExplainPlan()` to see which path a query takes:

```csharp
var command = (DbfDbCommand)dbConnection.CreateCommand();
command.CommandText = "select * from setup.dbf where KEY_NAME = 'CONTACTS'";
Console.WriteLine(command.ExplainPlan()); // index seek (=) on tag 'KEY_NAME'
```

### Connection string options

The connection string supports the options available in `DbfDataReaderOptions`:

| Option | Required | Type | Default | Description |
| --- | --- | --- | --- | --- |
| `Folder` | yes | string | — | The folder containing the files to be queried |
| `Encoding` | no | string | `null` (uses the language from the DBF header) | A valid encoding web name from [Encoding](https://learn.microsoft.com/en-us/dotnet/api/System.Text.Encoding), e.g. `ascii` |
| `ReadFloatsAsDecimals` | no | boolean | `false` | Whether to read floats as decimals |
| `SkipDeletedRecords` | no | boolean | `true` | Whether to skip deleted records |
| `StringTrimming` | no | string | `None` | String trimming behaviour: one of `None`, `Trim`, `TrimStart`, `TrimEnd` |
| `UseIndexes` | no | boolean | `true` | Whether to use sidecar `.cdx` compound indexes automatically |

## Used by

- [DbfBulkCopy](https://github.com/yellowfeather/DbfBulkCopy) — command line application
  to bulk copy from DBF files to MS SQL Server
- [dbf](https://github.com/yellowfeather/dbf) — command line utility to display DBF info
  and contents

## License

DbfDataReader is released under the [MIT License](LICENSE).
