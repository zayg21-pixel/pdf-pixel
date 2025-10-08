using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Models
{
    public class PdfDictionary
    {
        private readonly Dictionary<string, IPdfValue> _values = new Dictionary<string, IPdfValue>();

        public PdfDictionary(PdfDocument document)
        {
            Document = document;
        }

        public int Count => _values.Count;

        internal Dictionary<string, IPdfValue> RawValues => _values;

        public PdfDocument Document { get; }

        public bool HasKey(string key)
        {
            return _values.ContainsKey(GetValidKey(key));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return key[0] == '/' ? key : "/" + key;
        }

        public string GetName(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsName() : null;

        public string GetString(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsString() : null;

        public bool GetBoolOrDefault(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                return resolvedValue != null && resolvedValue.AsBool();
            }
            return false;
        }

        public int? GetInt(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);

                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    return resolvedValue.AsInteger();
                }
            }

            return default;
        }

        public float? GetFloat(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    return resolvedValue.AsFloat();
                }
            }

            return default;
        }

        public bool? GetBool(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    return resolvedValue.AsBool();
                }
            }

            return default;
        }

        public int GetIntegerOrDefault(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsInteger() ?? 0 : 0;


        public float GetFloatOrDefault(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsFloat() ?? 0 : 0;

        private bool IsCollectionType(IPdfValue value)
        {
            return value.Type == PdfValueType.Array || value.Type == PdfValueType.Dictionary;
        }

        public IPdfValue GetValue(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                return storedValue.ResolveToNonReference(Document);
            }

            return null;
        }

        public PdfArray GetArray(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                return storedValue.ResolveToNonReference(Document).AsArray();
            }

            return null;
        }

        public PdfDictionary GetDictionary(string key) =>
            _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsDictionary() : null;

        public PdfObject GetPageObject(string key)
        {
            if (!_values.TryGetValue(GetValidKey(key), out var storedValue))
                return null;

            // Array of references case
            var referenceArray = storedValue.AsArray();
            if (referenceArray != null)
            {
                return referenceArray.GetPageObject(0);
            }

            // Single reference case: return the existing object from the document to keep stream data
            if (storedValue is IPdfValue<PdfReference> referenceValue)
            {
                var reference = referenceValue.Value;
                return Document.GetObject(reference);
            }

            return null;
        }

        public List<PdfObject> GetPageObjects(string key)
        {
            if (!_values.TryGetValue(GetValidKey(key), out var storedValue))
                return null;

            var results = new List<PdfObject>();
            var referenceArray = storedValue.ResolveToNonReference(Document)?.AsArray();

            if (referenceArray != null)
            {
                for (int i = 0; i < referenceArray.Count; i++)
                {
                    var item = referenceArray.GetPageObject(i);

                    if (item != null)
                    {
                        results.Add(item);
                    }
                }

                return results;
            }

            // Single item fallback
            var single = GetPageObject(key);
            if (single != null) results.Add(single);
            return results.Count > 0 ? results : null;
        }

        // Internal methods for parsing
        internal void Set(string key, IPdfValue value)
        {
            _values[GetValidKey(key)] = value;
        }
    }
}