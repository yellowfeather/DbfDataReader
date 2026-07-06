using System;
using System.Linq;
using DbfDataReader.Cdx;
using Shouldly;
using Xunit;

namespace DbfDataReader.Tests;

public class CdxKeyEncoderTests
{
    [Theory]
    [InlineData(0, new byte[] { 0x80, 0x00, 0x00, 0x00 })]
    [InlineData(1, new byte[] { 0x80, 0x00, 0x00, 0x01 })]
    [InlineData(256, new byte[] { 0x80, 0x00, 0x01, 0x00 })]
    [InlineData(-1, new byte[] { 0x7F, 0xFF, 0xFF, 0xFF })]
    [InlineData(int.MaxValue, new byte[] { 0xFF, 0xFF, 0xFF, 0xFF })]
    [InlineData(int.MinValue, new byte[] { 0x00, 0x00, 0x00, 0x00 })]
    public void Should_encode_integers(int value, byte[] expected)
    {
        CdxKeyEncoder.EncodeInteger(value).ShouldBe(expected);
        CdxKeyEncoder.DecodeInteger(expected).ShouldBe(value);
    }

    [Fact]
    public void Integer_key_byte_order_should_match_value_order()
    {
        var values = new[] { int.MinValue, -100000, -256, -1, 0, 1, 2, 255, 256, 100000, int.MaxValue };
        var keys = values.Select(CdxKeyEncoder.EncodeInteger).ToList();

        for (var i = 1; i < keys.Count; i++)
        {
            CompareBytes(keys[i - 1], keys[i]).ShouldBeLessThan(0, $"{values[i - 1]} vs {values[i]}");
        }
    }

    [Fact]
    public void Should_encode_doubles_round_trip()
    {
        var values = new[] { double.MinValue, -1e10, -1.5, -1e-10, 0.0, 1e-10, 0.5, 1.0, 1e10, double.MaxValue };

        foreach (var value in values)
        {
            var key = CdxKeyEncoder.EncodeDouble(value);
            key.Length.ShouldBe(8);
            CdxKeyEncoder.DecodeDouble(key).ShouldBe(value);
        }
    }

    [Fact]
    public void Should_encode_known_doubles()
    {
        // 1.0 = 0x3FF0... big-endian, sign bit flipped
        CdxKeyEncoder.EncodeDouble(1.0)
            .ShouldBe(new byte[] { 0xBF, 0xF0, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
        // -1.0 = 0xBFF0... fully complemented
        CdxKeyEncoder.EncodeDouble(-1.0)
            .ShouldBe(new byte[] { 0x40, 0x0F, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF });
        CdxKeyEncoder.EncodeDouble(0.0)
            .ShouldBe(new byte[] { 0x80, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 });
    }

    [Fact]
    public void Double_key_byte_order_should_match_value_order()
    {
        var values = new[] { -1e100, -2.0, -1.0, -0.5, -1e-10, 0.0, 1e-10, 0.5, 1.0, 2.0, 1e100 };
        var keys = values.Select(CdxKeyEncoder.EncodeDouble).ToList();

        for (var i = 1; i < keys.Count; i++)
        {
            CompareBytes(keys[i - 1], keys[i]).ShouldBeLessThan(0, $"{values[i - 1]} vs {values[i]}");
        }
    }

    [Theory]
    [InlineData(1970, 1, 1, 2440588)] // well-known Julian day number anchors
    [InlineData(2000, 1, 1, 2451545)]
    [InlineData(1, 1, 1, 1721426)]
    public void Should_convert_dates_to_julian_day_numbers(int year, int month, int day, int expectedJulianDay)
    {
        CdxKeyEncoder.ToJulianDay(new DateTime(year, month, day)).ShouldBe(expectedJulianDay);
    }

    [Fact]
    public void Should_include_the_time_of_day_as_a_fraction()
    {
        CdxKeyEncoder.ToJulianDay(new DateTime(2000, 1, 1, 12, 0, 0)).ShouldBe(2451545.5);
    }

    [Fact]
    public void Date_key_byte_order_should_match_date_order()
    {
        var dates = new[]
        {
            new DateTime(1899, 12, 30), new DateTime(1970, 1, 1), new DateTime(1997, 6, 15),
            new DateTime(2000, 1, 1), new DateTime(2026, 7, 6)
        };
        var keys = dates.Select(CdxKeyEncoder.EncodeDate).ToList();

        for (var i = 1; i < keys.Count; i++)
        {
            CompareBytes(keys[i - 1], keys[i]).ShouldBeLessThan(0);
        }
    }

    private static int CompareBytes(byte[] x, byte[] y)
    {
        for (var i = 0; i < x.Length; i++)
        {
            var cmp = x[i].CompareTo(y[i]);
            if (cmp != 0) return cmp;
        }

        return 0;
    }
}
