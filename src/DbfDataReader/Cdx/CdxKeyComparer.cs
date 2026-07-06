namespace DbfDataReader.Cdx
{
    internal static class CdxKeyComparer
    {
        private const byte Pad = 0x20;

        // Compares a stored index key against a full-length target key. Stored leaf keys have
        // their trailing padding trimmed, so missing trailing bytes compare as the pad byte -
        // otherwise a stored key that is a strict prefix of the target would compare as equal.
        public static int Compare(byte[] storedKey, byte[] targetKey)
        {
            for (var i = 0; i < targetKey.Length; i++)
            {
                var storedByte = i < storedKey.Length ? storedKey[i] : Pad;
                var cmp = storedByte.CompareTo(targetKey[i]);
                if (cmp != 0) return cmp;
            }

            return 0;
        }
    }
}
