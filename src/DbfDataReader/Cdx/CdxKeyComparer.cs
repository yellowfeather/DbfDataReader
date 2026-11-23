namespace DbfDataReader.Cdx
{
    internal static class CdxKeyComparer
    {
        private const byte CharacterPad = 0x20;

        // Compares a stored index key against a full-length target key. Stored leaf keys have
        // their trailing padding trimmed, so missing trailing bytes compare as the pad byte -
        // otherwise a stored key that is a strict prefix of the target would compare as equal.
        // Character keys pad with spaces; binary keys (integer, double, date) pad with zeros.
        public static int Compare(byte[] storedKey, byte[] targetKey)
        {
            return Compare(storedKey, targetKey, CharacterPad);
        }

        public static int Compare(byte[] storedKey, byte[] targetKey, byte pad)
        {
            for (var i = 0; i < targetKey.Length; i++)
            {
                var storedByte = i < storedKey.Length ? storedKey[i] : pad;
                var cmp = storedByte.CompareTo(targetKey[i]);
                if (cmp != 0) return cmp;
            }

            return 0;
        }
    }
}
