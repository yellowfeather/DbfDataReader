using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DbfDataReader.Tests;

// Writes a minimal dBASE III table (character and numeric columns only), for cases no
// fixture covers, such as column names that are not valid identifiers.
public static class DbfTableBuilder
{
    public static readonly (string Name, char Type, byte Length)[] CoverageColumns =
    {
        ("Coverage %", 'N', 5),
        ("Name (x)", 'C', 12),
    };

    public static readonly string[][] CoverageRows =
    {
        new[] { "10", "alpha" },
        new[] { "75", "bravo" },
        new[] { "40", "charlie" },
        new[] { "100", "delta" },
    };

    public static MemoryStream CreateCoverageTable() => new MemoryStream(Build(CoverageColumns, CoverageRows));

    public static byte[] Build((string Name, char Type, byte Length)[] columns, IReadOnlyList<string[]> rows)
    {
        var recordLength = 1;
        foreach (var column in columns)
        {
            recordLength += column.Length;
        }

        var headerLength = 32 + 32 * columns.Length + 1;

        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII);

        writer.Write((byte)0x03);
        writer.Write(new byte[] { 126, 1, 1 }); // last update: 2026-01-01
        writer.Write(rows.Count);
        writer.Write((short)headerLength);
        writer.Write((short)recordLength);
        writer.Write(new byte[20]);

        foreach (var column in columns)
        {
            if (column.Name.Length > 10) throw new ArgumentException($"Column name '{column.Name}' is too long.");

            var descriptor = new byte[32];
            Encoding.ASCII.GetBytes(column.Name, 0, column.Name.Length, descriptor, 0);
            descriptor[11] = (byte)column.Type;
            descriptor[16] = column.Length;
            writer.Write(descriptor);
        }

        writer.Write((byte)0x0D);

        foreach (var row in rows)
        {
            writer.Write((byte)' ');

            for (var i = 0; i < columns.Length; i++)
            {
                var value = columns[i].Type == 'N'
                    ? row[i].PadLeft(columns[i].Length)
                    : row[i].PadRight(columns[i].Length);
                writer.Write(Encoding.ASCII.GetBytes(value));
            }
        }

        writer.Write((byte)0x1A);
        writer.Flush();

        return stream.ToArray();
    }
}
