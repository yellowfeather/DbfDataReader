using System;

namespace DbfDataReader.Cdx
{
    public class CdxException : Exception
    {
        public CdxException(CdxErrorCode code)
            : base($"Invalid CDX index file: {code}")
        {
            Code = code;
        }

        public CdxErrorCode Code { get; }
    }

    public enum CdxErrorCode
    {
        None,
        NotACompoundIndexHeader,
        RootNodeDoesNotHaveRootAttribute,
        LeftmostNodeHasLeftSibling,
        InvalidNodeAttributes,
        InvalidIndexOptions,
        InvalidExpressionPoolLength,
        InvalidInteriorNodeKeyCount,
        InvalidInteriorNodeSibling,
        InvalidLeafNodeKeyCount,
        InvalidLeafNodeCalculatedKeyStartIndex,
        FirstLeafNodeKeyEntryHasDuplicateBytes,
        PackedKeyEntryLengthTooLong,
        InteriorNodeHasNoKeyEntries,
        NodeChainTooLong
    }
}
