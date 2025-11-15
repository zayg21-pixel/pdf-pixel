using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfReader.Models
{
    /// <summary>
    /// Strongly-typed PDF array wrapper providing type-safe indexed access.
    /// </summary>
    public class PdfArray
    {
        private readonly IPdfValue[] _items;

        public PdfArray(PdfDocument document, IPdfValue[] items)
        {
            Document = document;
            _items = items ?? Array.Empty<IPdfValue>();
        }

        public PdfArray(PdfDocument document, IList<IPdfValue> items)
        {
            Document = document;
            _items = items?.ToArray() ?? Array.Empty<IPdfValue>();
        }

        /// <summary>
        /// Raw values for internal access.
        /// </summary>
        internal IList<IPdfValue> RawValues => _items;

        /// <summary>
        /// Owning document used for reference resolution.
        /// </summary>
        public PdfDocument Document { get; }

        /// <summary>
        /// Number of items in the array.
        /// </summary>
        public int Count => _items.Length;

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
        public PdfString GetName(int index)
        {
            var value = GetValue(index);
            if (value == null)
            {
                return default;
            }

            return value.AsName();
        }

        /// <summary>
        /// Get a string value at an index (text, hex, or name coerced). Returns null if not a string-like value.
        /// </summary>
        public PdfString GetString(int index)
        {
            var value = GetValue(index);

            if (value == null)
            {
                return default;
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
        public bool GetBoolean(int index)
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

            return value.AsArray();
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
        public PdfObject GetObject(int index)
        {
            if (!IsValidIndex(index))
            {
                return null;
            }

            var storedValue = _items[index];

            // Array of references case
            var referenceArray = storedValue.AsArray();
            if (referenceArray != null)
            {
                return referenceArray.GetObject(0);
            }

            // Single reference case
            if (storedValue is IPdfValue<PdfReference> referenceValue)
            {
                var reference = referenceValue.Value;
                return Document.GetObject(reference);
            }

            // return synthetic object
            return new PdfObject(default, Document, storedValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < _items.Length;
        }
    }
}
