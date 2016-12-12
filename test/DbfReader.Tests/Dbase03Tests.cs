﻿using Shouldly;
using Xunit;

namespace DbfReader.Tests
{
    public class Dbase03Tests : DbaseTests
    {
        private const string Dbase03FixturePath = "./test/fixtures/dbase_03.dbf";

        public Dbase03Tests() : base(Dbase03FixturePath)
        {
        }

        [Fact]
        public void Should_report_correct_record_count()
        {
            DbfHeader.RecordCount.ShouldBe(14);
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
            DbfTable.Columns.Count.ShouldBe(31);
        }

        [Fact]
        public void Should_have_the_correct_column_schema()
        {
            ValidateColumnSchema("./test/fixtures/dbase_03_summary.txt");
        }

        [Fact]
        public void Should_have_correct_row_values()
        {
            ValidateRowValues("./test/fixtures/dbase_03.csv");
        }
    }
}