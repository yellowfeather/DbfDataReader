﻿using Shouldly;
using Xunit;

namespace DbfDataReader.Tests
{
    [Collection("tl_2019_01_place")]
    public class EsriShapefileTests : DbaseTests
    {
        private const string Dbase03FixturePath = "../../../../fixtures/tl_2019_01_place.dbf";

        public EsriShapefileTests() : base(Dbase03FixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(586);
        }

        [Fact]
        public void Should_report_correct_version_number()
        {
            DbfHeader.Version.ShouldBe(0x03);
        }

        [Fact]
        public void Should_report_that_the_file_is_not_foxpro()
        {
            DbfHeader.IsFoxPro.ShouldBeFalse();
        }

        [Fact]
        public void Should_have_the_correct_number_of_columns()
        {
            DbfTable.Columns.Count.ShouldBe(16);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("../../../../fixtures/tl_2019_01_place_summary.txt");
        }

        [Fact]
        [UseCulture("en-GB")]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("../../../../fixtures/tl_2019_01_place.csv");
        }
    }
}
