using System;

namespace DbfDataReader.Cdx
{
    [Flags]
    public enum CdxIndexOptions : byte
    {
        None = 0,
        Unique = 1,
        // observed in real-world files (e.g. custom indexes), mentioned but not described in most CDX documentation
        CustomIndex = 4,
        HasForClause = 8,
        BitVector = 16,
        IsCompactIndex = 32,
        IsCompoundIndexHeader = 64,
        IsStructuralIndex = 128,

        All = Unique | CustomIndex | HasForClause | BitVector | IsCompactIndex | IsCompoundIndexHeader |
              IsStructuralIndex
    }

    public enum CdxIndexOrder
    {
        Ascending = 0,
        Descending = 1
    }

    [Flags]
    internal enum CdxNodeAttributes
    {
        InteriorNode = 0,
        RootNode = 1,
        LeafNode = 2,
        // observed in real-world files, undocumented
        Unknown = 4,

        All = RootNode | LeafNode | Unknown
    }
}
