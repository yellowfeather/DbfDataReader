using System.Text;

namespace DbfDataReader
{
    public static class EncodingProvider
    {
        static EncodingProvider()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        }

        public static Encoding GetEncoding(int codePage)
        {
            return Encoding.GetEncoding(codePage);
        }

        public static Encoding GetEncoding(byte languageDriver)
        {
            // Table from dbf.py by Ethan Furman at https://github.com/ethanfurman/dbf/blob/master/dbf/__init__.py,
            // mixed with table at https://github.com/olemb/dbfread/blob/master/dbfread/codepages.py
            // and https://github.com/infused/dbf/blob/master/lib/dbf/encodings.rb
            switch (languageDriver)
            {
                case 0x00:
                    return Encoding.ASCII; //  20127, "ascii", "plain ol' ascii"
                case 0x01:
                    return Encoding.GetEncoding(437); // "cp437", "U.S. MS-DOS"
                case 0x02:
                    return Encoding.GetEncoding(850); // "cp850", "International MS-DOS"
                case 0x03:
                    return Encoding.GetEncoding(1252); // "cp1252", "Windows ANSI"
                case 0x04:
                    return Encoding.GetEncoding(1000); // "mac_roman", "Standard Macintosh"
                case 0x08:
                    return Encoding.GetEncoding(865); // "cp865", "Danish OEM"
                case 0x09:
                    return Encoding.GetEncoding(437); //  "cp437", "Dutch OEM"
                case 0x0A:
                    return Encoding.GetEncoding(850); //  "cp850", "Dutch OEM (secondary)"
                case 0x0B:
                    return Encoding.GetEncoding(437); //  "cp437", "Finnish OEM"
                case 0x0D:
                    return Encoding.GetEncoding(437); //  "cp437", "French OEM"
                case 0x0E:
                    return Encoding.GetEncoding(850); //  "cp850", "French OEM (secondary)"
                case 0x0F:
                    return Encoding.GetEncoding(437); //  "cp437", "German OEM"
                case 0x10:
                    return Encoding.GetEncoding(850); //  "cp850", "German OEM (secondary)"
                case 0x11:
                    return Encoding.GetEncoding(437); //  "cp437", "Italian OEM"
                case 0x12:
                    return Encoding.GetEncoding(850); //  "cp850", "Italian OEM (secondary)"
                case 0x13:
                    return Encoding.GetEncoding(932); //  "cp932", "Japanese Shift-JIS"
                case 0x14:
                    return Encoding.GetEncoding(850); //  "cp850", "Spanish OEM (secondary)"
                case 0x15:
                    return Encoding.GetEncoding(437); //  "cp437", "Swedish OEM"
                case 0x16:
                    return Encoding.GetEncoding(850); //  "cp850", "Swedish OEM (secondary)"
                case 0x17:
                    return Encoding.GetEncoding(865); //  "cp865", "Norwegian OEM"
                case 0x18:
                    return Encoding.GetEncoding(437); //  "cp437", "Spanish OEM"
                case 0x19:
                    return Encoding.GetEncoding(437); //  "cp437", "English OEM (Britain)"
                case 0x1A:
                    return Encoding.GetEncoding(850); //  "cp850", "English OEM (Britain) (secondary)"
                case 0x1B:
                    return Encoding.GetEncoding(437); //  "cp437", "English OEM (U.S.)"
                case 0x1C:
                    return Encoding.GetEncoding(863); //  "cp863", "French OEM (Canada)"
                case 0x1D:
                    return Encoding.GetEncoding(850); //  "cp850", "French OEM (secondary)"
                case 0x1F:
                    return Encoding.GetEncoding(852); //  "cp852", "Czech OEM"
                case 0x22:
                    return Encoding.GetEncoding(852); //  "cp852", "Hungarian OEM"
                case 0x23:
                    return Encoding.GetEncoding(852); //  "cp852", "Polish OEM"
                case 0x24:
                    return Encoding.GetEncoding(860); //  "cp860", "Portuguese OEM"
                case 0x25:
                    return Encoding.GetEncoding(850); //  "cp850", "Portuguese OEM (secondary)"
                case 0x26:
                    return Encoding.GetEncoding(866); //  "cp866", "Russian OEM"
                case 0x37:
                    return Encoding.GetEncoding(850); //  "cp850", "English OEM (U.S.) (secondary)"
                case 0x40:
                    return Encoding.GetEncoding(852); //  "cp852", "Romanian OEM"
                case 0x4D:
                    return Encoding.GetEncoding(936); //  "cp936", "Chinese GBK (PRC)"
                case 0x4E:
                    return Encoding.GetEncoding(949); //  "cp949", "Korean (ANSI/OEM)"
                case 0x4F:
                    return Encoding.GetEncoding(950); //  "cp950", "Chinese Big 5 (Taiwan)"
                case 0x50:
                    return Encoding.GetEncoding(874); //  "cp874", "Thai (ANSI/OEM)"
                case 0x57:
                    return Encoding.GetEncoding(1252); //  "cp1252", "ANSI"
                case 0x58:
                    return Encoding.GetEncoding(1252); //  "cp1252", "Western European ANSI"
                case 0x59:
                    return Encoding.GetEncoding(1252); //  "cp1252", "Spanish ANSI"
                case 0x64:
                    return Encoding.GetEncoding(852); //  "cp852", "Eastern European MS-DOS"
                case 0x65:
                    return Encoding.GetEncoding(866); //  "cp866", "Russian MS-DOS"
                case 0x66:
                    return Encoding.GetEncoding(865); //  "cp865", "Nordic MS-DOS"
                case 0x67:
                    return Encoding.GetEncoding(861); //  "cp861", "Icelandic MS-DOS"
                case 0x68:
                    return Encoding.GetEncoding(895); // Kamenicky (Czech) MS-DOS
                case 0x69:
                    return Encoding.GetEncoding(620); //  "Mazovia (Polish) MS-DOS"
                case 0x6a:
                    return Encoding.GetEncoding(737); //  "cp737", "Greek MS-DOS (437G)"
                case 0x6b:
                    return Encoding.GetEncoding(857); //  "cp857", "Turkish MS-DOS"
                case 0x6c:
                    return Encoding.GetEncoding(863); //  "cp863", "French-Canadian MS-DOS"
                case 0x78:
                    return Encoding.GetEncoding(950); //  "cp950", "Traditional Chinese (Hong Kong SAR, Taiwan) Windows"
                case 0x79:
                    return Encoding.GetEncoding(949); //  "cp949", "Korean Windows"
                case 0x7a:
                    return Encoding.GetEncoding(936); //  "cp936", "Chinese Simplified (PRC, Singapore) Windows"
                case 0x7b:
                    return Encoding.GetEncoding(932); //  "cp932", "Japanese Windows"
                case 0x7c:
                    return Encoding.GetEncoding(874); //  "cp874", "Thai Windows"
                case 0x7d:
                    return Encoding.GetEncoding(1255); //  "cp1255", "Hebrew Windows"
                case 0x7e:
                    return Encoding.GetEncoding(1256); //  "cp1256", "Arabic Windows"
                case 0x86:
                    return Encoding.GetEncoding(737); //  "cp737", "Greek OEM"
                case 0x87:
                    return Encoding.GetEncoding(852); //  "cp852", "Slovenian OEM"
                case 0x88:
                    return Encoding.GetEncoding(857); //  "cp857", "Turkish OEM"
                case 0x96:
                    return Encoding.GetEncoding(10007); //  "mac_cyrillic", "Russian Macintosh"
                case 0x97:
                    return Encoding.GetEncoding(10029); //  "mac_latin2", "Macintosh EE"
                case 0x98:
                    return Encoding.GetEncoding(10006); //  "mac_greek", "Greek Macintosh"
                case 0xc8:
                    return Encoding.GetEncoding(1250); //  "cp1250", "Eastern European Windows"
                case 0xc9:
                    return Encoding.GetEncoding(1251); //  "cp1251", "Russian Windows"
                case 0xca:
                    return Encoding.GetEncoding(1254); //  "cp1254", "Turkish Windows"
                case 0xcb:
                    return Encoding.GetEncoding(1253); //  "cp1253", "Greek Windows"
                case 0xcc:
                    return Encoding.GetEncoding(1257); //  "cp1257", "Baltic Windows"
                case 0xf0:
                    return Encoding.UTF8; //  "utf8", "8-bit unicode"
                default:
                    return Encoding.GetEncoding(1252); // Unable to guess encoding for language driver
            }
        }
    }
}