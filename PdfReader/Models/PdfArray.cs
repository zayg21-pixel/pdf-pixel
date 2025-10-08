using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace PdfReader.Models
{
    /// <summary>
    /// Strongly-typed PDF array wrapper providing type-safe indexed access.
    /// </summary>
    public class PdfArray
    {
        private readonly List<IPdfValue> _items = new List<IPdfValue>();

        public PdfArray(PdfDocument document, List<IPdfValue> items)
        {
            Document = document;
            _items = items ?? new List<IPdfValue>();
        }

        /// <summary>
        /// Raw values for internal access.
        /// </summary>
        internal List<IPdfValue> RawValues => _items;

        /// <summary>
        /// Owning document used for reference resolution.
        /// </summary>
        public PdfDocument Document { get; }

        /// <summary>
        /// Number of items in the array.
        /// </summary>
        public int Count => _items.Count;

        /// <summary>
        /// Get the raw (resolved) value at an index or null if out of range.
        /// Follows references to the first non-reference value.
        /// </summary>
        public IPdfValue GetValue(int index)
        {
            if (!IsValidIndex(index))
            {
                return null;
            }

            var storedValue = _items[index];
            return storedValue.ResolveToNonReference(Document);
        }

        /// <summary>
        /// Get a name value (e.g. /Subtype) at an index. Returns null if not a name.
        /// </summary>
        public string GetName(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return null;
            }
            return value.AsName();
        }

        /// <summary>
        /// Get a string value at an index (text, hex, or name coerced). Returns null if not a string-like value.
        /// </summary>
        public string GetString(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return null;
            }
            return value.AsString();
        }

        /// <summary>
        /// Get an integer value at an index or 0 if not numeric.
        /// </summary>
        public int GetInteger(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return 0;
            }
            return value.AsInteger();
        }

        /// <summary>
        /// Get a float value at an index or 0 if not numeric.
        /// </summary>
        public float GetFloat(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return 0f;
            }
            return value.AsFloat();
        }

        /// <summary>
        /// Get a boolean value (interprets names /true /false, numbers, and strings heuristically).
        /// </summary>
        public bool GetBool(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return false;
            }
            return value.AsBool();
        }

        /// <summary>
        /// Get an inner array as a resolved list of values. Returns null if not an array.
        /// </summary>
        public PdfArray GetArray(int index)
        {
            var value = GetValue(index);

            if (value == null)
            {
                return null;
            }

            throw new NotImplementedException();

            //return value.AsArray();
        }

        /// <summary>
        /// Get an inner dictionary value. Returns null if not a dictionary.
        /// </summary>
        public PdfDictionary GetDictionary(int index)
        {
            var value = GetValue(index);

            if (value == null)
            {
                return null;
            }

            return value.AsDictionary();
        }

        /// <summary>
        /// Get a page object (resolves reference, arrays-of-references to first, or wraps inline value).
        /// </summary>
        public PdfObject GetPageObject(int index)
        {
            if (!IsValidIndex(index) || Document?.Objects == null)
            {
                return null;
            }

            var storedValue = _items[index];

            // Array of references case
            var referenceArray = storedValue.AsArray();
            if (referenceArray != null)
            {
                return referenceArray.GetPageObject(0);
            }

            // Single reference case
            if (storedValue is IPdfValue<PdfReference> referenceValue)
            {
                var reference = referenceValue.Value;
                if (reference.IsValid && Document.Objects.TryGetValue(reference.ObjectNumber, out var referencedObj))
                {
                    return referencedObj;
                }
                return null;
            }

            return null;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < _items.Count;
        }
    }
}
