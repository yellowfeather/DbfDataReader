using DbfDataReader.Cdx;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    public class CdxKeyPackingTests
    {
        [Theory]
        [InlineData(0, 0, 0x00_00_00_00_00_00_00_00L)]
        [InlineData(0, 1, 0x00_00_00_00_00_00_00_00L)]
        [InlineData(0, 2, 0x00_00_00_00_00_00_01_00L)]
        [InlineData(0, 3, 0x00_00_00_00_00_02_01_00L)]
        [InlineData(0, 4, 0x00_00_00_00_03_02_01_00L)]
        [InlineData(0, 5, 0x00_00_00_04_03_02_01_00L)]
        [InlineData(0, 6, 0x00_00_05_04_03_02_01_00L)]
        [InlineData(0, 7, 0x00_06_05_04_03_02_01_00L)]
        [InlineData(0, 8, 0x07_06_05_04_03_02_01_00L)]
        [InlineData(1, 8, 0x08_07_06_05_04_03_02_01L)]
        [InlineData(2, 8, 0x09_08_07_06_05_04_03_02L)]
        [InlineData(3, 8, 0x0A_09_08_07_06_05_04_03L)]
        [InlineData(3, 7, 0x00_09_08_07_06_05_04_03L)]
        [InlineData(3, 6, 0x00_00_08_07_06_05_04_03L)]
        [InlineData(3, 5, 0x00_00_00_07_06_05_04_03L)]
        [InlineData(3, 4, 0x00_00_00_00_06_05_04_03L)]
        public void Should_read_packed_entries_correctly(int startIndex, int length, long expected)
        {
            var buffer = new byte[488];
            for (var i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (byte)(i % 256);
            }

            CdxKeyPacking.ReadPackedEntry(buffer, startIndex, length).ShouldBe(expected);
        }
    }
}
