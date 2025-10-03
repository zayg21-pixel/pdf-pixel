using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Models
{
    // Type-safe PDF dictionary
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

        // Ensure dictionary key has leading slash
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static string GetValidKey(string key)
        {
            if (string.IsNullOrEmpty(key)) return key;
            return key[0] == '/' ? key : "/" + key;
        }

        // Type-safe getters with common PDF patterns
        public string GetName(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsName() : null;

        public string GetString(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsString() : null;

        public int GetInteger(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsInteger() ?? 0 : 0;

        public float GetFloat(string key)
            => _values.TryGetValue(GetValidKey(key), out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsFloat() ?? 0 : 0;

        public bool GetBool(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                return resolvedValue != null && resolvedValue.AsBool();
            }
            return false;
        }

        public bool TryGetInt(string key, out int value)
        {
            value = 0;
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    value = resolvedValue.AsInteger();
                    return true;
                }
            }
            return false;
        }

        public bool TryGetFloat(string key, out float value)
        {
            value = 0f;
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    value = resolvedValue.AsFloat();
                    return true;
                }
            }
            return false;
        }

        public bool TryGetBool(string key, out bool value)
        {
            value = false;
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                var resolvedValue = storedValue.ResolveToNonReference(Document);
                if (resolvedValue != null && !IsCollectionType(resolvedValue))
                {
                    value = resolvedValue.AsBool();
                    return true;
                }
            }
            return false;
        }

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

        public List<IPdfValue> GetArray(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                return storedValue.ResolveToArray(Document);
            }

            return null;
        }

        public PdfDictionary GetDictionary(string key)
        {
            if (_values.TryGetValue(GetValidKey(key), out var storedValue))
            {
                return storedValue.ResolveToDictionary(Document);
            }

            return null;
        }

        // Universal page object helpers
        // If a reference array is found for a single-object accessor, use the first item if present
        public PdfObject GetPageObject(string key)
        {
            if (!_values.TryGetValue(GetValidKey(key), out var storedValue) || Document?.Objects == null)
                return null;

            // Array of references case
            var referenceArray = storedValue.AsReferenceArray();
            if (referenceArray != null)
            {
                if (referenceArray.Count > 0 && Document.Objects.TryGetValue(referenceArray[0].ObjectNumber, out var pdfObject))
                {
                    return pdfObject;
                }
                return null;
            }

            // Single reference case: return the existing object from the document to keep stream data
            if (storedValue.Type == PdfValueType.Reference)
            {
                var reference = storedValue.AsReference();
                if (reference.IsValid && Document.Objects.TryGetValue(reference.ObjectNumber, out var referencedObj))
                {
                    return referencedObj;
                }
                return null;
            }

            // Inline dictionary or other inline value: wrap as a transient PdfObject (no stream)
            var inlineValue = storedValue.ResolveToNonReference(Document) ?? storedValue;
            return new PdfObject(new PdfReference(0), Document, inlineValue);
        }

        // If a single reference is found where an array is expected, return a collection of one item
        public List<PdfObject> GetPageObjects(string key)
        {
            if (!_values.TryGetValue(GetValidKey(key), out var storedValue) || Document?.Objects == null)
                return null;

            var results = new List<PdfObject>();
            var references = storedValue.AsReferenceArray();

            if (references == null && storedValue.Type == PdfValueType.Reference)
            {
                var reference = storedValue.AsReference();
                if (Document.Objects.TryGetValue(reference.ObjectNumber, out var pdfObject) && pdfObject != null)
                {
                    references = pdfObject.Value.AsReferenceArray();
                }    
            }

            if (references != null)
            {
                foreach (var reference in references)
                {
                    if (Document.Objects.TryGetValue(reference.ObjectNumber, out var pdfObject) && pdfObject != null)
                        results.Add(pdfObject);
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