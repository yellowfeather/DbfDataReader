using System.Text;
using DbfDataReader.Cdx;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    public class CdxKeyComparerTests
    {
        [Theory]
        [InlineData("ABC", "ABC", 0)]
        [InlineData("AB ", "AB ", 0)]
        [InlineData("ABC", "ABD", -1)]
        [InlineData("ABD", "ABC", 1)]
        [InlineData("ABC", "AB ", 1)]
        public void Should_compare_full_length_keys(string stored, string target, int expectedSign)
        {
            var cmp = CdxKeyComparer.Compare(Encoding.ASCII.GetBytes(stored), Encoding.ASCII.GetBytes(target));

            if (expectedSign == 0)
                cmp.ShouldBe(0);
            else if (expectedSign < 0)
                cmp.ShouldBeLessThan(0);
            else
                cmp.ShouldBeGreaterThan(0);
        }

        [Fact]
        public void Should_treat_trimmed_trailing_bytes_as_padding()
        {
            // stored leaf keys are trimmed of trailing spaces; "AB" is the stored form of "AB "
            var stored = Encoding.ASCII.GetBytes("AB");

            CdxKeyComparer.Compare(stored, Encoding.ASCII.GetBytes("AB ")).ShouldBe(0);
        }

        [Fact]
        public void Should_not_match_a_stored_key_that_is_a_prefix_of_the_target()
        {
            // "AB" (stored form of "AB ") sorts before "ABC" and must not compare as equal to it
            var stored = Encoding.ASCII.GetBytes("AB");

            CdxKeyComparer.Compare(stored, Encoding.ASCII.GetBytes("ABC")).ShouldBeLessThan(0);
        }
    }
}
