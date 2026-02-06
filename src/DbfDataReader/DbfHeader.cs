using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfHeader
    {
        public const int DbfHeaderSize = 32;

        public DbfHeader(Stream stream)
        {
            var buffer = new byte[DbfHeaderSize];
#if NETSTANDARD2_1
            stream.Read(buffer, 0, DbfHeaderSize);
#else
            stream.ReadExactly(buffer, 0, DbfHeaderSize);
#endif
            var span = new ReadOnlySpan<byte>(buffer);
            Read(span);
        }

        public int Version { get; private set; }

        public DateTime UpdatedAt { get; private set; }
        public int HeaderLength { get; private set; }
        public int RecordLength { get; private set; }
        public long RecordCount { get; private set; }
        public byte LanguageDriver { get; private set; }

        public string VersionDescription
        {
            get
            {
                string description;
                switch (Version)
                {
                    case 0x02:
                        description = "FoxPro";
                        break;
                    case 0x03:
                        description = "dBase III without memo file";
                        break;
                    case 0x04:
                        description = "dBase IV without memo file";
                        break;
                    case 0x05:
                        description = "dBase V without memo file";
                        break;
                    case 0x07:
                        description = "Visual Objects 1.x";
                        break;
                    case 0x30:
                        description = "Visual FoxPro";
                        break;
                    case 0x31:
                        description = "Visual FoxPro with AutoIncrement field";
                        break;
                    case 0x43:
                        description = "dBASE IV SQL table files, no memo";
                        break;
                    case 0x63:
                        description = "dBASE IV SQL system files, no memo";
                        break;
                    case 0x7b:
                        description = "dBase IV with memo file";
                        break;
                    case 0x83:
                        description = "dBase III with memo file";
                        break;
                    case 0x87:
                        description = "Visual Objects 1.x with memo file";
                        break;
                    case 0x8b:
                        description = "dBase IV with memo file";
                        break;
                    case 0x8e:
                        description = "dBase IV with SQL table";
                        break;
                    case 0xcb:
                        description = "dBASE IV SQL table files, with memo";
                        break;
                    case 0xf5:
                        description = "FoxPro with memo file";
                        break;
                    case 0xfb:
                        description = "FoxPro without memo file";
                        break;
                    default:
                        description = "Unknown";
                        break;
                }

                return description;
            }
        }

        public bool IsFoxPro => Version == 0x30 || Version == 0x31 || Version == 0xf5 || Version == 0xfb;

        public void Read(ReadOnlySpan<byte> bytes)
        {
            Version = bytes[0];

            var year = bytes[1];
            var month = bytes[2];
            var day = bytes[3];

            UpdatedAt = new DateTime(year + 1900, month, day);

            RecordCount = BitConverter.ToUInt32(bytes[4..]);
            HeaderLength = BitConverter.ToUInt16(bytes[8..]);
            RecordLength = BitConverter.ToUInt16(bytes[10..]);

            // See https://www.clicketyclick.dk/databases/xbase/format/dbf.html

            // 12 - 13  - reserved
            // 14       - incomplete transaction
            // 15       - encryption flag
            // 16 - 19  - free record thread
            // 20 - 27  - reserved for multi-user dbase
            // 28       - MDX flag

            LanguageDriver = bytes[29];

            // 30 - 31  - reserved
        }
    }
}