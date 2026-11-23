using System;
using System.IO;
using System.Linq;
using DbfDataReader.Cdx;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("foxprodb")]
    public class CdxTests
    {
        private const string FixturesPath = "../../../../fixtures/foxprodb";

        public static TheoryData<string> CdxFixtures => new TheoryData<string>
        {
            "calls.CDX",
            "contacts.CDX",
            "setup.CDX",
            "types.CDX",
            "FOXPRO-DB-TEST.DCX"
        };

        [Theory]
        [MemberData(nameof(CdxFixtures))]
        public void Should_enumerate_tags(string fixtureName)
        {
            using var cdxFile = OpenCdx(fixtureName);

            cdxFile.TagNames.ShouldNotBeEmpty();

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);

                index.ShouldNotBeNull();
                index.Header.KeyLength.ShouldBeGreaterThan(0);
                index.KeyExpression.ShouldNotBeNullOrEmpty();
            }
        }

        [Theory]
        [MemberData(nameof(CdxFixtures))]
        public void Should_enumerate_entries_in_ascending_key_order(string fixtureName)
        {
            using var cdxFile = OpenCdx(fixtureName);

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);
                if (index.Header.Order != CdxIndexOrder.Ascending) continue;

                var entries = index.EnumerateEntries().ToList();
                entries.Count.ShouldBe(index.Count(), $"tag: {tagName}");

                for (var i = 1; i < entries.Count; i++)
                {
                    ComparePadded(entries[i - 1].KeyBytes, entries[i].KeyBytes, index.Header.KeyLength)
                        .ShouldBeLessThanOrEqualTo(0, $"tag: {tagName}, entry: {i}");
                }
            }
        }

        [Theory]
        [MemberData(nameof(CdxFixtures))]
        public void Should_find_entries_by_exact_key(string fixtureName)
        {
            using var cdxFile = OpenCdx(fixtureName);
            var searches = 0;

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);
                if (index.Header.Order != CdxIndexOrder.Ascending) continue;

                var entries = index.EnumerateEntries().ToList();
                if (entries.Count == 0) continue;

                var samples = new[] { entries[0], entries[entries.Count / 2], entries[entries.Count - 1] };
                foreach (var sample in samples)
                {
                    var target = PadKey(sample.KeyBytes, index.Header.KeyLength);
                    var results = index.Search(target).ToList();

                    results.ShouldContain(e => e.RecordNumber == sample.RecordNumber, $"tag: {tagName}");
                    results.Count.ShouldBe(
                        entries.Count(e => PaddedEquals(e.KeyBytes, sample.KeyBytes, index.Header.KeyLength)),
                        $"tag: {tagName}");

                    searches++;
                }
            }

            searches.ShouldBeGreaterThan(0);
        }

        [Theory]
        [MemberData(nameof(CdxFixtures))]
        public void Should_return_no_entries_for_missing_keys(string fixtureName)
        {
            using var cdxFile = OpenCdx(fixtureName);

            foreach (var tagName in cdxFile.TagNames)
            {
                var index = cdxFile.GetIndex(tagName);
                if (index.Header.Order != CdxIndexOrder.Ascending) continue;

                var keyLength = index.Header.KeyLength;
                var entries = index.EnumerateEntries().ToList();

                var probes = new[] { CreateKey(0x01, keyLength), CreateKey(0xFE, keyLength) };
                foreach (var probe in probes)
                {
                    if (entries.Any(e => PaddedEquals(e.KeyBytes, probe, keyLength))) continue;

                    index.Search(probe).ShouldBeEmpty($"tag: {tagName}");
                }
            }
        }

        [Fact]
        public void Should_find_dbf_rows_from_index_entries()
        {
            var fixtures = new[]
            {
                ("calls.CDX", "calls.dbf"),
                ("contacts.CDX", "contacts.dbf"),
                ("setup.CDX", "setup.dbf"),
                ("types.CDX", "types.dbf")
            };

            var validatedTags = 0;

            foreach (var (cdxName, dbfName) in fixtures)
            {
                using var dbfTable = new DbfTable(Path.Combine(FixturesPath, dbfName),
                    stringTrimming: StringTrimmingOption.TrimEnd);
                using var cdxFile = new CdxFile(Path.Combine(FixturesPath, cdxName), dbfTable.CurrentEncoding);

                var dbfRecord = new DbfRecord(dbfTable);

                foreach (var tagName in cdxFile.TagNames)
                {
                    var index = cdxFile.GetIndex(tagName);
                    if (index.Header.Order != CdxIndexOrder.Ascending) continue;

                    // only tags whose key expression is a plain character column can be
                    // validated against row values without evaluating index expressions
                    var column = dbfTable.Columns.FirstOrDefault(c =>
                        string.Equals(c.ColumnName, index.KeyExpression, StringComparison.OrdinalIgnoreCase));
                    if (column == null || column.ColumnType != DbfColumnType.Character) continue;

                    var ordinal = dbfTable.Columns.IndexOf(column);
                    var entries = index.EnumerateEntries().ToList();
                    var step = Math.Max(1, entries.Count / 100);

                    for (var i = 0; i < entries.Count; i += step)
                    {
                        var entry = entries[i];

                        dbfTable.Seek(entry.RecordIndex);
                        dbfTable.Read(dbfRecord).ShouldBeTrue($"tag: {tagName}, record: {entry.RecordNumber}");

                        var value = dbfRecord.GetStringValue(ordinal) ?? string.Empty;
                        value.ShouldBe(entry.Key.TrimEnd(), $"tag: {tagName}, record: {entry.RecordNumber}");
                    }

                    validatedTags++;
                }
            }

            validatedTags.ShouldBeGreaterThan(0);
        }

        private static CdxFile OpenCdx(string fixtureName)
        {
            return new CdxFile(Path.Combine(FixturesPath, fixtureName));
        }

        private static byte[] CreateKey(byte fill, int length)
        {
            var key = new byte[length];
            for (var i = 0; i < key.Length; i++)
            {
                key[i] = fill;
            }

            return key;
        }

        private static byte[] PadKey(byte[] key, int length)
        {
            if (key.Length == length) return key;

            var padded = CreateKey(0x20, length);
            Array.Copy(key, padded, key.Length);
            return padded;
        }

        private static bool PaddedEquals(byte[] x, byte[] y, int length)
        {
            return ComparePadded(x, y, length) == 0;
        }

        private static int ComparePadded(byte[] x, byte[] y, int length)
        {
            var paddedX = PadKey(x, length);
            var paddedY = PadKey(y, length);

            for (var i = 0; i < length; i++)
            {
                var cmp = paddedX[i].CompareTo(paddedY[i]);
                if (cmp != 0) return cmp;
            }

            return 0;
        }
    }
}
