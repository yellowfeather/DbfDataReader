# DbfDataReader

[![Build status](https://ci.appveyor.com/api/projects/status/pe6p1rhi3g305cpq?svg=true)](https://ci.appveyor.com/project/chrisrichards/dbfdatareader)
[![NuGet](https://img.shields.io/nuget/v/DbfDataReader.svg)](https://www.nuget.org/packages/DbfDataReader/)
[![MyGet Build Status](https://www.myget.org/BuildSource/Badge/dbfdatareader?identifier=54ae0096-55d5-418c-8eb9-54a35df720fb)](https://www.myget.org/)

DbfDataReader is a small fast .Net Core library for reading dBase, xBase, Clipper and FoxPro database files

Usage, to get summary info:

```csharp
var dbfPath = "path/file.dbf";
using (var dbfTable = new DbfTable(dbfPath))
{
    var header = dbfTable.Header;

    var versionDescription = header.VersionDescription;
    var hasMemo = dbfTable.Memo != null;
    var recordCount = header.RecordCount;

    foreach (var dbfColumn in dbfTable.Columns)
    {
        var name = dbfColumn.Name;
        var columnType = dbfColumn.ColumnType;
        var length = dbfColumn.Length;
        var decimalCount = dbfColumn.DecimalCount;
    }
}
```

and to iterate over the rows:

```csharp
var skipDeleted = true;

var dbfPath = "path/file.dbf";
using (var dbfTable = new DbfTable(dbfPath))
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

There is also an implementation of DbDataReader:

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

which also means you can bulk copy to MS SqlServer:

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

Used by 

- DbfBulkCopy

    Command line application to bulk copy from DBF files to MS SqlServer
    
    https://github.com/yellowfeather/DbfBulkCopy

- dbf

    Command line utility to display DBF info and contents
    
    https://github.com/yellowfeather/dbf
