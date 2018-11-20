using System;
using System.IO;

namespace DbfDataReader
{
    public class DbfHeader
    {
        private const int DbfHeaderSize = 32;

        public DbfHeader(BinaryReader binaryReader)
        {
            Read(binaryReader);
        }

        public int Version { get; private set; }

        public DateTime UpdatedAt { get; private set; }
        public int HeaderLength { get; private set; }
        public int RecordLength { get; private set; }
        public long RecordCount { get; private set; }

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

        public void Read(BinaryReader binaryReader)
        {
            Version = binaryReader.ReadByte();

            var year = binaryReader.ReadByte();
            var month = binaryReader.ReadByte();
            var day = binaryReader.ReadByte();

            UpdatedAt = new DateTime(year + 1900, month, day);

            RecordCount = binaryReader.ReadUInt32();
            HeaderLength = binaryReader.ReadUInt16();
            RecordLength = binaryReader.ReadUInt16();

            // skip the reserved bytes
            binaryReader.ReadBytes(20);
        }
    }
}