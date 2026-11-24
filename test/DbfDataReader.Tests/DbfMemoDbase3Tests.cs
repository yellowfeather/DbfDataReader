using System;
using System.IO;
using System.Linq;
using System.Text;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

// issue #349: a dBASE III memo ends at the first 0x1A marker, not at a block boundary
public class DbfMemoDbase3Tests
{
    private const int BlockSize = 512;
    private const byte EndOfMemo = 0x1a;
    private const string Dbase83FixturePath = "../../../../fixtures/dbase_83.dbf";

    private static readonly Encoding Ascii = Encoding.ASCII;

    // builds a memo file: a header block followed by each memo, terminated by two
    // end-of-memo markers and padded to a block boundary with the padding byte
    private static MemoryStream BuildMemoFile(byte padding, params byte[][] memos)
    {
        var stream = new MemoryStream();
        stream.Write(new byte[BlockSize], 0, BlockSize);

        foreach (var memo in memos)
        {
            stream.Write(memo, 0, memo.Length);
            stream.WriteByte(EndOfMemo);
            stream.WriteByte(EndOfMemo);
            while (stream.Length % BlockSize != 0) stream.WriteByte(padding);
        }

        stream.Position = 0;
        return stream;
    }

    private static long BlocksFor(int memoLength)
    {
        return (memoLength + 2 + BlockSize - 1) / BlockSize;
    }

    [Theory]
    [InlineData((byte)0x00)] // NUL padding, as in the file from issue #349
    [InlineData((byte)'x')]  // stale data after the marker
    [InlineData((byte)' ')]
    public void Should_stop_at_the_end_of_memo_marker(byte padding)
    {
        using var stream = BuildMemoFile(padding, Ascii.GetBytes("first memo"), Ascii.GetBytes("second memo"));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe("first memo");
        memo.Get(2).ShouldBe("second memo");
    }

    [Theory]
    [InlineData(510)]  // marker fills the rest of the first block
    [InlineData(511)]  // marker straddles the block boundary
    [InlineData(512)]  // marker starts the second block
    [InlineData(513)]
    [InlineData(2000)]
    public void Should_read_a_memo_spanning_blocks(int length)
    {
        var text = string.Concat(Enumerable.Range(0, length).Select(i => (char)('a' + i % 26)));
        using var stream = BuildMemoFile(0x00, Ascii.GetBytes(text), Ascii.GetBytes("next memo"));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe(text);
        memo.Get(1 + BlocksFor(length)).ShouldBe("next memo");
    }

    [Fact]
    public void Should_keep_spaces_at_a_block_boundary()
    {
        // the first block ends with a space and the second starts with one
        var text = new string('a', 511) + "  " + new string('b', 100);
        using var stream = BuildMemoFile(0x00, Ascii.GetBytes(text));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe(text);
    }

    [Fact]
    public void Should_decode_a_multi_byte_character_split_across_blocks()
    {
        // 511 single byte characters, then a two byte character over the block boundary
        var text = new string('a', 511) + "é" + new string('b', 10);
        using var stream = BuildMemoFile(0x00, Encoding.UTF8.GetBytes(text));
        using var memo = new DbfMemoDbase3(stream, Encoding.UTF8);

        memo.Get(1).ShouldBe(text);
    }

    [Fact]
    public void Should_read_a_shorter_memo_after_a_longer_one()
    {
        var longText = new string('a', 1500);
        using var stream = BuildMemoFile(0x00, Ascii.GetBytes(longText), Ascii.GetBytes("short"));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe(longText);
        memo.Get(1 + BlocksFor(longText.Length)).ShouldBe("short");
        memo.Get(1).ShouldBe(longText);
    }

    [Fact]
    public void Should_trim_surrounding_spaces()
    {
        using var stream = BuildMemoFile(0x00, Ascii.GetBytes("  padded  "));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe("padded");
    }

    [Fact]
    public void Should_return_empty_for_an_empty_block()
    {
        using var stream = new MemoryStream(new byte[BlockSize * 2]);
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_stop_at_a_nul_when_the_marker_is_missing()
    {
        var bytes = new byte[BlockSize * 3];
        Ascii.GetBytes("no marker").CopyTo(bytes, BlockSize);
        Ascii.GetBytes("next memo").CopyTo(bytes, BlockSize * 2);
        using var stream = new MemoryStream(bytes);
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe("no marker");
    }

    [Fact]
    public void Should_stop_at_the_end_of_the_file_when_unterminated()
    {
        var bytes = new byte[BlockSize + 700];
        Array.Fill(bytes, (byte)'a', BlockSize, 700);
        using var stream = new MemoryStream(bytes);
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(1).ShouldBe(new string('a', 700));
    }

    [Fact]
    public void Should_return_empty_for_a_block_past_the_end_of_the_file()
    {
        using var stream = BuildMemoFile(0x00, Ascii.GetBytes("only memo"));
        using var memo = new DbfMemoDbase3(stream, Ascii);

        memo.Get(50).ShouldBe(string.Empty);
    }

    [Fact]
    public void Should_read_a_multi_block_memo_from_a_dbase_83_file()
    {
        const string expected =
            "Our Original assortment...a little taste of heaven for everyone.  Let us\r\n" +
            "select a special assortment of our chocolate and pastel favorites for you.\r\n" +
            "Each petit four is its own special hand decorated creation. Multi-layers of\r\n" +
            "moist cake with combinations of specialty fillings create memorable cake\r\n" +
            "confections. Varietes include; Luscious Lemon, Strawberry Hearts, White\r\n" +
            "Chocolate, Mocha Bean, Roasted Almond, Triple Chocolate, Chocolate Hazelnut,\r\n" +
            "Grand Orange, Plum Squares, Milk chocolate squares, and Raspberry Blanc.";

        using var dbfDataReader = new DbfDataReader(Dbase83FixturePath);
        var memoOrdinal = dbfDataReader.DbfTable.Columns
            .First(column => column.ColumnType == DbfColumnType.Memo).ColumnOrdinal.Value;

        dbfDataReader.Read().ShouldBeTrue();
        dbfDataReader.GetString(memoOrdinal).ShouldBe(expected);
    }

    [Fact]
    public void Should_not_read_past_the_end_of_any_memo_in_a_dbase_83_file()
    {
        using var dbfDataReader = new DbfDataReader(Dbase83FixturePath);
        var memoOrdinals = dbfDataReader.DbfTable.Columns
            .Where(column => column.ColumnType == DbfColumnType.Memo)
            .Select(column => column.ColumnOrdinal.Value)
            .ToArray();

        var rows = 0;
        while (dbfDataReader.Read())
        {
            rows++;
            foreach (var ordinal in memoOrdinals)
            {
                var value = dbfDataReader.GetString(ordinal);
                value.ShouldNotContain((char)EndOfMemo);
                value.ShouldNotContain('\0');
            }
        }

        rows.ShouldBe(67);
    }
}
