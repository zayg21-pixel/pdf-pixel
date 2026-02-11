using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfPixel.Models;

/// <summary>
/// Represents a strongly-typed wrapper for a PDF array, providing type-safe indexed access to PDF values.
/// </summary>
/// <remarks>
/// This class enables safe and convenient access to PDF array elements, including type coercion and reference resolution.
/// </remarks>
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
    /// Gets the owning <see cref="PdfDocument"/> used for reference resolution.
    /// </summary>
    public PdfDocument Document { get; }

    /// <summary>
    /// Gets the number of items in the array.
    /// </summary>
    public int Count => _items.Length;

    /// <summary>
    /// Gets the raw (resolved) value at the specified index, or <c>null</c> if the index is out of range.
    /// Follows references to the first non-reference value.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The resolved <see cref="IPdfValue"/> at the specified index, or <c>null</c> if out of range.</returns>
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
    /// Gets a name value (e.g. <c>/Subtype</c>) at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> if the value is a name; otherwise, <c>null</c>.</returns>
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
    /// Gets a string value at the specified index (text, hex, or name coerced).
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> if the value is string-like; otherwise, <c>null</c>.</returns>
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
    /// Gets an integer value at the specified index, or <c>null</c> if the value is not numeric.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The integer value if present; otherwise, <c>null</c>.</returns>
    public int? GetInteger(int index)
    {
        var value = GetValue(index);

        if (value == null)
        {
            return default;
        }

        if (value.Type == PdfValueType.Integer || value.Type == PdfValueType.Real)
        {
            return value.AsInteger();
        }

        return default;
    }

    /// <summary>
    /// Gets an integer value at the specified index, or 0 if the value is not numeric.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The integer value if present; otherwise, 0.</returns>
    public int GetIntegerOrDefault(int index)
    {
        var value = GetValue(index);
        if (value == null)
        {
            return 0;
        }
        return value.AsInteger();
    }

    /// <summary>
    /// Gets a floating-point value at the specified index, or <c>null</c> if the value is not numeric.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The float value if present; otherwise, <c>null</c>.</returns>
    public float? GetFloat(int index)
    {
        var value = GetValue(index);

        if (value == null)
        {
            return 0f;
        }

        if (value.Type == PdfValueType.Integer || value.Type == PdfValueType.Real)
        {
            return value.AsFloat();
        }

        return default;
    }

    /// <summary>
    /// Gets a floating-point value at the specified index, or 0 if the value is not numeric.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The float value if present; otherwise, 0.</returns>
    public float GetFloatOrDefault(int index)
    {
        var value = GetValue(index);
        if (value == null)
        {
            return 0f;
        }
        return value.AsFloat();
    }

    /// <summary>
    /// Gets a boolean value at the specified index, or <c>null</c> if the value is not a boolean.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The boolean value if present; otherwise, <c>null</c>.</returns>
    public bool? GetBoolean(int index)
    {
        var value = GetValue(index);
        if (value == null)
        {
            return default;
        }

        if (value.Type == PdfValueType.Boolean)
        {
            return value.AsBoolean();
        }

        return default;
    }


    /// <summary>
    /// Gets a boolean value at the specified index, interpreting names <c>/true</c> and <c>/false</c> as booleans.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The boolean value if present; otherwise, <c>false</c>.</returns>
    public bool GetBooleanOrDefault(int index)
    {
        var value = GetValue(index);
        if (value == null)
        {
            return false;
        }
        return value.AsBoolean();
    }

    /// <summary>
    /// Gets an inner array as a resolved list of values at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The <see cref="PdfArray"/> if the value is an array; otherwise, <c>null</c>.</returns>
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
    /// Gets an inner dictionary value at the specified index.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The <see cref="PdfDictionary"/> if the value is a dictionary; otherwise, <c>null</c>.</returns>
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
    /// Gets a page object at the specified index, resolving references, arrays of references (to the first), or wrapping an inline value.
    /// </summary>
    /// <param name="index">The zero-based index of the value to retrieve.</param>
    /// <returns>The <see cref="PdfObject"/> at the specified index, or <c>null</c> if the index is invalid.</returns>
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
            return Document.ObjectCache.GetObject(reference);
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
