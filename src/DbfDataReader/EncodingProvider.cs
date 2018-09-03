using System.Text;

namespace DbfDataReader
{
    public static class EncodingProvider
    {
        static EncodingProvider()
        {
#if NETSTANDARD1_6_1
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
#endif
        }

        public static Encoding UTF8 => Encoding.UTF8;
        public static Encoding UTF7 => Encoding.UTF7;
        public static Encoding UTF32 => Encoding.UTF32;
        public static Encoding ASCII => Encoding.ASCII;
        public static Encoding BigEndianUnicode => Encoding.BigEndianUnicode;
        public static Encoding Unicode => Encoding.Unicode;

        public static Encoding GetEncoding(int codePage)
        {
            return Encoding.GetEncoding(codePage);
        }
    }
}