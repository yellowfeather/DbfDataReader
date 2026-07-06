using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;

namespace DbfDataReader
{
    public class DbfDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbfDbParameter> _parameters = new List<DbfDbParameter>();

        public override int Count => _parameters.Count;

        public override object SyncRoot => ((ICollection)_parameters).SyncRoot;

        public DbfDbParameter AddWithValue(string parameterName, object value)
        {
            var parameter = new DbfDbParameter { ParameterName = parameterName, Value = value };
            _parameters.Add(parameter);
            return parameter;
        }

        public override int Add(object value)
        {
            _parameters.Add(Cast(value));
            return _parameters.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public override void Clear()
        {
            _parameters.Clear();
        }

        public override bool Contains(object value)
        {
            return value is DbfDbParameter parameter && _parameters.Contains(parameter);
        }

        public override bool Contains(string value)
        {
            return IndexOf(value) >= 0;
        }

        public override void CopyTo(Array array, int index)
        {
            ((ICollection)_parameters).CopyTo(array, index);
        }

        public override IEnumerator GetEnumerator()
        {
            return _parameters.GetEnumerator();
        }

        protected override DbParameter GetParameter(int index)
        {
            return _parameters[index];
        }

        protected override DbParameter GetParameter(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index < 0) throw new ArgumentException($"Parameter '{parameterName}' was not found.");

            return _parameters[index];
        }

        public override int IndexOf(object value)
        {
            return value is DbfDbParameter parameter ? _parameters.IndexOf(parameter) : -1;
        }

        public override int IndexOf(string parameterName)
        {
            for (var index = 0; index < _parameters.Count; index++)
            {
                if (NamesEqual(_parameters[index].ParameterName, parameterName)) return index;
            }

            return -1;
        }

        public override void Insert(int index, object value)
        {
            _parameters.Insert(index, Cast(value));
        }

        public override void Remove(object value)
        {
            if (value is DbfDbParameter parameter) _parameters.Remove(parameter);
        }

        public override void RemoveAt(int index)
        {
            _parameters.RemoveAt(index);
        }

        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) _parameters.RemoveAt(index);
        }

        protected override void SetParameter(int index, DbParameter value)
        {
            _parameters[index] = Cast(value);
        }

        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0)
            {
                _parameters.Add(Cast(value));
            }
            else
            {
                _parameters[index] = Cast(value);
            }
        }

        // parameter names match case-insensitively, with or without a leading '@'
        private static bool NamesEqual(string x, string y)
        {
            return string.Equals(Normalize(x), Normalize(y), StringComparison.OrdinalIgnoreCase);
        }

        internal static string Normalize(string parameterName)
        {
            return parameterName != null && parameterName.StartsWith("@", StringComparison.Ordinal)
                ? parameterName.Substring(1)
                : parameterName;
        }

        private static DbfDbParameter Cast(object value)
        {
            if (value is DbfDbParameter parameter) return parameter;

            throw new InvalidCastException($"The value must be a {nameof(DbfDbParameter)}.");
        }
    }
}
