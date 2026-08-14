using System;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a strongly-typed PDF dictionary, providing type-safe access to values and reference resolution.
/// </summary>
public class PdfDictionary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class with the specified document and optional values.
    /// </summary>
    /// <param name="document">The owning PDF document.</param>
    internal PdfDictionary(IPdfDocumentInternal document)
        : this(document, null)
    {
        Document = document;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class with the specified document and values.
    /// </summary>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="values">The initial dictionary values.</param>
    internal PdfDictionary(IPdfDocumentInternal document, Dictionary<PdfString, IPdfValue>? values)
    {
        Document = document;
        RawValues = values ?? new Dictionary<PdfString, IPdfValue>();
    }

    /// <summary>
    /// Gets the number of entries in the dictionary.
    /// </summary>
    public int Count => RawValues.Count;

    /// <summary>
    /// Gets the owning PDF document.
    /// </summary>
    internal IPdfDocumentInternal Document { get; }

    /// <summary>
    /// Gets the raw dictionary values. Internal use only.
    /// </summary>
    internal Dictionary<PdfString, IPdfValue> RawValues { get; }

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the dictionary contains the key; otherwise, <c>false</c>.</returns>
    public bool HasKey(in PdfString key) => RawValues.ContainsKey(key);

    /// <summary>
    /// Gets the resolved value for the specified key, following references.
    /// Returns <c>null</c> if the key is not present.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The resolved <see cref="IPdfValue"/> or <c>null</c>.</returns>
    public IPdfValue? GetValue(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            return storedValue.ResolveToNonReference(Document);
        }

        return null;
    }

    /// <summary>
    /// Gets the value as a PDF name for the specified key, following references.
    /// Returns <c>null</c> if not present or not a name.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> name value, or <c>null</c>.</returns>
    public PdfString? GetName(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue)) ? storedValue.ResolveToNonReference(Document)?.AsName() : null;

    /// <summary>
    /// Gets the value as a PDF name for the specified key, following references.
    /// Returns an empty <see cref="PdfString"/> if not present or not a name.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> name value, or an empty <see cref="PdfString"/>.</returns>
    public PdfString GetNameOrDefault(in PdfString key) => GetName(key) ?? PdfString.Empty;

    /// <summary>
    /// Gets the value as a PDF string for the specified key, following references.
    /// Returns <c>null</c> if not present or not a string.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> value, or <c>null</c>.</returns>
    public PdfString? GetString(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue)) ? storedValue.ResolveToNonReference(Document)?.AsString() : null;

    /// <summary>
    /// Gets the value as a PDF string for the specified key, following references.
    /// Returns an empty <see cref="PdfString"/> if not present or not a string.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> value, or an empty <see cref="PdfString"/>.</returns>
    public PdfString GetStringOrDefault(in PdfString key) => GetString(key) ?? PdfString.Empty;

    /// <summary>
    /// Gets the value as an integer for the specified key, following references.
    /// Returns <c>null</c> if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The integer value, or <c>null</c>.</returns>
    public int? GetInteger(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            IPdfValue? resolvedValue = storedValue.ResolveToNonReference(Document);

            if (resolvedValue != null && (resolvedValue.Type == PdfValueType.Integer || resolvedValue.Type == PdfValueType.Real))
            {
                return resolvedValue.AsInteger();
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the value as an integer for the specified key, following references.
    /// Returns 0 if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The integer value, or 0.</returns>
    public int GetIntegerOrDefault(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue)) ? storedValue.ResolveToNonReference(Document)?.AsInteger() ?? 0 : 0;

    /// <summary>
    /// Gets the value as a float for the specified key, following references.
    /// Returns <c>null</c> if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The float value, or <c>null</c>.</returns>
    public float? GetFloat(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            IPdfValue? resolvedValue = storedValue.ResolveToNonReference(Document);
            if (resolvedValue != null && (resolvedValue.Type == PdfValueType.Integer || resolvedValue.Type == PdfValueType.Real))
            {
                return resolvedValue.AsFloat();
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the value as a float for the specified key, following references.
    /// Returns 0 if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The float value, or 0.</returns>
    public float GetFloatOrDefault(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue)) ? storedValue.ResolveToNonReference(Document)?.AsFloat() ?? 0 : 0;

    /// <summary>
    /// Gets the value as a boolean for the specified key, following references.
    /// Returns <c>null</c> if not present or not a boolean.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The boolean value, or <c>null</c>.</returns>
    public bool? GetBoolean(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            IPdfValue? resolvedValue = storedValue.ResolveToNonReference(Document);
            if (resolvedValue != null && resolvedValue.Type == PdfValueType.Boolean)
            {
                return resolvedValue.AsBoolean();
            }
        }

        return default;
    }

    /// <summary>
    /// Gets the value as a boolean for the specified key, following references.
    /// Returns <c>false</c> if not present or not a boolean.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The boolean value, or <c>false</c>.</returns>
    public bool GetBooleanOrDefault(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            IPdfValue? resolvedValue = storedValue.ResolveToNonReference(Document);
            return resolvedValue?.AsBoolean() == true;
        }

        return false;
    }

    /// <summary>
    /// Gets the value as a PDF array for the specified key, following references.
    /// Returns <c>null</c> if not present or not an array.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfArray"/> value, or <c>null</c>.</returns>
    public PdfArray? GetArray(in PdfString key)
    {
        if (RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            return storedValue.ResolveToNonReference(Document)?.AsArray();
        }

        return null;
    }

    /// <summary>
    /// Gets the value as a PDF dictionary for the specified key, following references.
    /// Returns <c>null</c> if not present or not a dictionary.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfDictionary"/> value, or <c>null</c>.</returns>
    public PdfDictionary? GetDictionary(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue)) ? storedValue.ResolveToNonReference(Document)?.AsDictionary() : null;

    /// <summary>
    /// Gets the value as a PDF object for the specified key, following references.
    /// Handles single references, and inline values.
    /// Returns <c>null</c> if not present.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfObject"/> value, or <c>null</c>.</returns>
    public PdfObject? GetObject(in PdfString key)
    {
        if (!RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            return null;
        }

        // Single reference case: return the existing object from the document to keep stream data
        if (storedValue is IPdfValue<PdfReference> referenceValue)
        {
            PdfReference reference = referenceValue.Value;
            return Document.ObjectCache.GetObject(reference);
        }

        // return synthetic object
        return new PdfObject(default, Document, storedValue);
    }

    /// <summary>
    /// Gets the reference the specified key holds.
    /// Returns <c>null</c> if not present or if the entry is a direct value.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfReference"/> value, or <c>null</c>.</returns>
    public PdfReference? GetReference(in PdfString key)
        => (RawValues.TryGetValue(key, out IPdfValue? storedValue) && storedValue is IPdfValue<PdfReference> referenceValue) ? referenceValue.Value : null;

    /// <summary>
    /// Gets a list of PDF objects for the specified key, handling arrays of references and single items.
    /// Returns <c>null</c> if not present or no objects found.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>A list of <see cref="PdfObject"/> values, or <c>null</c>.</returns>
    public List<PdfObject>? GetObjects(in PdfString key)
    {
        if (!RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            return null;
        }

        List<PdfObject> results = [];
        PdfArray? referenceArray = storedValue.ResolveToNonReference(Document)?.AsArray();

        if (referenceArray != null)
        {
            for (int i = 0; i < referenceArray.Count; i++)
            {
                PdfObject? item = referenceArray.GetObject(i);

                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }

        // Single item fallback
        PdfObject? single = GetObject(key);
        if (single != null)
        {
            results.Add(single);
        }

        return (results.Count > 0) ? results : null;
    }

    /// <summary>
    /// Sets the value for the specified key. Internal use only for parsing.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to assign.</param>
    internal void Set(in PdfString key, IPdfValue value) => RawValues[key] = value;

    /// <summary>
    /// Returns a new dictionary combining this dictionary's entries with <paramref name="other"/>'s,
    /// with entries from <paramref name="other"/> taking precedence for any key present in both.
    /// </summary>
    /// <param name="other">The dictionary whose entries take precedence when merging.</param>
    /// <returns>A new merged <see cref="PdfDictionary"/>.</returns>
    public PdfDictionary MergeWith(PdfDictionary other)
    {
        if (other == null)
        {
            throw new ArgumentNullException(nameof(other));
        }

        PdfDictionary merged = new(Document);

        foreach (KeyValuePair<PdfString, IPdfValue> entry in RawValues)
        {
            merged.Set(entry.Key, entry.Value);
        }

        foreach (KeyValuePair<PdfString, IPdfValue> entry in other.RawValues)
        {
            merged.Set(entry.Key, entry.Value);
        }

        return merged;
    }
}
