using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics;
using System.Text;

namespace DbfDataReader
{
    public class DbfDbConnectionStringBuilder : DbConnectionStringBuilder
    {
        private enum Keywords
        {
            Encoding,
            Folder,
            ReadFloatsAsDecimals,
            SkipDeletedRecords,
            StringTrimming,
            
            // keep the count value last
            KeywordsCount
        }

        internal const int KeywordsCount = (int)Keywords.KeywordsCount;
        
        private static readonly string[] ValidKeywords;
        private static readonly Dictionary<string,Keywords> KeywordsHash;

        private Encoding _encoding;
        private string _folder = string.Empty;
        private bool _readFloatsAsDecimals;
        private bool _skipDeletedRecords = true;
        private StringTrimmingOption _stringTrimming = StringTrimmingOption.Trim;

        static DbfDbConnectionStringBuilder()
        {
            var validKeywords = new string[KeywordsCount];
            validKeywords[(int)Keywords.Encoding]             = DbfDbConnectionStringKeywords.Encoding;
            validKeywords[(int)Keywords.Folder]               = DbfDbConnectionStringKeywords.Folder;
            validKeywords[(int)Keywords.ReadFloatsAsDecimals] = DbfDbConnectionStringKeywords.ReadFloatsAsDecimals;
            validKeywords[(int)Keywords.SkipDeletedRecords]   = DbfDbConnectionStringKeywords.SkipDeletedRecords;
            validKeywords[(int)Keywords.StringTrimming]       = DbfDbConnectionStringKeywords.StringTrimming;
            ValidKeywords = validKeywords;

            var hash = new Dictionary<string, Keywords>(KeywordsCount, StringComparer.OrdinalIgnoreCase)
            {
                { DbfDbConnectionStringKeywords.Encoding, Keywords.Encoding },
                { DbfDbConnectionStringKeywords.Folder, Keywords.Folder },
                { DbfDbConnectionStringKeywords.ReadFloatsAsDecimals, Keywords.ReadFloatsAsDecimals },
                { DbfDbConnectionStringKeywords.SkipDeletedRecords, Keywords.SkipDeletedRecords },
                { DbfDbConnectionStringKeywords.StringTrimming, Keywords.StringTrimming }
            };
            Debug.Assert(KeywordsCount == hash.Count, "initial expected size is incorrect");
            KeywordsHash = hash;
        }
        
        public DbfDbConnectionStringBuilder() : this(null)
        {
        }

        public DbfDbConnectionStringBuilder(string connectionString)
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                ConnectionString = connectionString;
            }
        }

        public override object this[string keyword]
        {
            get
            {
                Keywords index = GetIndex(keyword);
                return GetAt(index);
            }
            set
            {
                if (null != value)
                {
                    Keywords index = GetIndex(keyword);
                    switch (index)
                    {
                        case Keywords.Encoding: Encoding = DbfDbConnectionStringBuilderUtil.ConvertToEncoding(keyword, value); break;
                        case Keywords.Folder: Folder = DbfDbConnectionStringBuilderUtil.ConvertToString(value); break;
                        case Keywords.ReadFloatsAsDecimals: ReadFloatsAsDecimals = DbfDbConnectionStringBuilderUtil.ConvertToBoolean(value); break;
                        case Keywords.SkipDeletedRecords: SkipDeletedRecords = DbfDbConnectionStringBuilderUtil.ConvertToBoolean(value); break;
                        case Keywords.StringTrimming: StringTrimming = DbfDbConnectionStringBuilderUtil.ConvertToStringTrimmingOption(keyword, value); break;
                        default:
                            Debug.Assert(false, "unexpected keyword");
                            throw DbfDbConnectionStringBuilderUtil.KeywordNotSupported(keyword);
                    }
                }
                else
                {
                    Remove(keyword);
                }
            }
        }
        
        public Encoding Encoding
        {
            get => _encoding;
            set
            {
                SetEncodingValue(value);
                _encoding = value;
            }
        }
        
        public string Folder
        {
            get => _folder;
            set
            {
                SetValue(DbfDbConnectionStringKeywords.Folder, value);
                _folder = value;
            }
        }
        
        public bool ReadFloatsAsDecimals
        {
            get => _readFloatsAsDecimals;
            set
            {
                SetValue(DbfDbConnectionStringKeywords.ReadFloatsAsDecimals, value);
                _readFloatsAsDecimals = value;
            }
        }
        
        public bool SkipDeletedRecords
        {
            get => _skipDeletedRecords;
            set
            {
                SetValue(DbfDbConnectionStringKeywords.SkipDeletedRecords, value);
                _skipDeletedRecords = value;
            }
        }
        
        public StringTrimmingOption StringTrimming
        {
            get => _stringTrimming;
            set
            {
                if (!DbfDbConnectionStringBuilderUtil.IsValidStringTrimmingOptionValue(value))
                {
                    throw DbfDbConnectionStringBuilderUtil.InvalidEnumerationValue(typeof(StringTrimmingOption), (int)value);
                }

                SetStringTrimmingValue(value);
                _stringTrimming = value;
            }
        }


        private void SetValue(string keyword, bool value)
        {
            base[keyword] = value.ToString();
        }
        
        private void SetValue(string keyword, string value)
        {
            DbfDbConnectionStringBuilderUtil.CheckArgumentNull(value, keyword);
            base[keyword] = value;
        }
        
        private void SetEncodingValue(Encoding value)
        {
            base[DbfDbConnectionStringKeywords.Encoding] = DbfDbConnectionStringBuilderUtil.EncodingToString(value);
        }

        private void SetStringTrimmingValue(StringTrimmingOption value)
        {
            Debug.Assert(DbfDbConnectionStringBuilderUtil.IsValidStringTrimmingOptionValue(value), "Invalid value for StringTrimming");
            base[DbfDbConnectionStringKeywords.StringTrimming] = DbfDbConnectionStringBuilderUtil.StringTrimmingOptionToString(value);
        }

        private object GetAt(Keywords index) 
        {
            switch(index)
            {
                case Keywords.Encoding:				return Encoding;
                case Keywords.Folder:				return Folder;
                case Keywords.ReadFloatsAsDecimals:	return ReadFloatsAsDecimals;
                case Keywords.SkipDeletedRecords:	return SkipDeletedRecords;
                case Keywords.StringTrimming:       return StringTrimming;
                default:
                    Debug.Assert(false, "unexpected keyword");
                    throw DbfDbConnectionStringBuilderUtil.KeywordNotSupported(ValidKeywords[(int)index]);
                }
        }

        private Keywords GetIndex(string keyword)
        {
            DbfDbConnectionStringBuilderUtil.CheckArgumentNull(keyword, "keyword");
            if (KeywordsHash.TryGetValue(keyword, out var index))
            {
                return index;
            }
            throw DbfDbConnectionStringBuilderUtil.KeywordNotSupported(keyword);            
        }

        public override bool Remove(string keyword)
        {
            DbfDbConnectionStringBuilderUtil.CheckArgumentNull(keyword, "keyword");
            if (KeywordsHash.TryGetValue(keyword, out var index))
            {
                if (base.Remove(ValidKeywords[(int)index]))
                {
                    Reset(index);
                    return true;
                }
            }
            return false;
        }

        private void Reset(Keywords index)
        {
            switch(index)
            {
                case Keywords.Encoding:
                    _encoding = null;
                    break;
                case Keywords.Folder:
                    _folder = string.Empty;
                    break;
                case Keywords.ReadFloatsAsDecimals:
                    _readFloatsAsDecimals = false;
                    break;
                case Keywords.SkipDeletedRecords:
                    _skipDeletedRecords = true;
                    break;
                case Keywords.StringTrimming:
                    _stringTrimming = StringTrimmingOption.Trim;
                    break;
                default:
                    Debug.Assert(false, "unexpected keyword");
                    throw DbfDbConnectionStringBuilderUtil.KeywordNotSupported(ValidKeywords[(int)index]);
            }
        }
        
        public override bool TryGetValue(string keyword, out object value)
        {
            if (KeywordsHash.TryGetValue(keyword, out var index))
            {
                value = GetAt(index);
                return true;
            }
            
            value = null;
            return false;
        }

    }
}
