using System.Collections.Generic;

namespace PdfRender.Models;

/// <summary>
/// Represents a strongly-typed PDF dictionary, providing type-safe access to values and reference resolution.
/// </summary>
public class PdfDictionary
{
    private readonly Dictionary<PdfString, IPdfValue> _values;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class with the specified document and optional values.
    /// </summary>
    /// <param name="document">The owning PDF document.</param>
    public PdfDictionary(PdfDocument document) : this(document, null)
    {
        Document = document;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDictionary"/> class with the specified document and values.
    /// </summary>
    /// <param name="document">The owning PDF document.</param>
    /// <param name="values">The initial dictionary values.</param>
    public PdfDictionary(PdfDocument document, Dictionary<PdfString, IPdfValue> values)
    {
        Document = document;
        _values = values ?? new Dictionary<PdfString, IPdfValue>();
    }

    /// <summary>
    /// Gets the number of entries in the dictionary.
    /// </summary>
    public int Count => _values.Count;

    /// <summary>
    /// Gets the owning PDF document.
    /// </summary>
    public PdfDocument Document { get; }

    /// <summary>
    /// Gets the raw dictionary values. Internal use only.
    /// </summary>
    internal Dictionary<PdfString, IPdfValue> RawValues => _values;

    /// <summary>
    /// Determines whether the dictionary contains the specified key.
    /// </summary>
    /// <param name="key">The key to locate.</param>
    /// <returns><c>true</c> if the dictionary contains the key; otherwise, <c>false</c>.</returns>
    public bool HasKey(PdfString key)
    {
        return _values.ContainsKey(key);
    }

    /// <summary>
    /// Gets the resolved value for the specified key, following references.
    /// Returns <c>null</c> if the key is not present.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The resolved <see cref="IPdfValue"/> or <c>null</c>.</returns>
    public IPdfValue GetValue(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            return storedValue.ResolveToNonReference(Document);
        }

        return null;
    }

    /// <summary>
    /// Gets the value as a PDF name for the specified key, following references.
    /// Returns <c>default</c> if not present or not a name.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> name value, or <c>default</c>.</returns>
    public PdfString GetName(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsName() ?? default : default;

    /// <summary>
    /// Gets the value as a PDF string for the specified key, following references.
    /// Returns <c>default</c> if not present or not a string.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfString"/> value, or <c>default</c>.</returns>
    public PdfString GetString(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsString() ?? default : default;

    /// <summary>
    /// Gets the value as an integer for the specified key, following references.
    /// Returns <c>null</c> if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The integer value, or <c>null</c>.</returns>
    public int? GetInteger(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);

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
    public int GetIntegerOrDefault(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsInteger() ?? 0 : 0;

    /// <summary>
    /// Gets the value as a float for the specified key, following references.
    /// Returns <c>null</c> if not present or not numeric.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The float value, or <c>null</c>.</returns>
    public float? GetFloat(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
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
    public float GetFloatOrDefault(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsFloat() ?? 0 : 0;

    /// <summary>
    /// Gets the value as a boolean for the specified key, following references.
    /// Returns <c>null</c> if not present or not a boolean.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The boolean value, or <c>null</c>.</returns>
    public bool? GetBoolean(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
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
    public bool GetBooleanOrDefault(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
            return resolvedValue != null && resolvedValue.AsBoolean();
        }
        return false;
    }

    /// <summary>
    /// Gets the value as a PDF array for the specified key, following references.
    /// Returns <c>null</c> if not present or not an array.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfArray"/> value, or <c>null</c>.</returns>
    public PdfArray GetArray(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            return storedValue.ResolveToNonReference(Document).AsArray();
        }

        return null;
    }

    /// <summary>
    /// Gets the value as a PDF dictionary for the specified key, following references.
    /// Returns <c>null</c> if not present or not a dictionary.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfDictionary"/> value, or <c>null</c>.</returns>
    public PdfDictionary GetDictionary(PdfString key) =>
        _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsDictionary() : null;

    /// <summary>
    /// Gets the value as a PDF object for the specified key, following references.
    /// Handles arrays of references, single references, and inline values.
    /// Returns <c>null</c> if not present.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>The <see cref="PdfObject"/> value, or <c>null</c>.</returns>
    public PdfObject GetObject(PdfString key)
    {
        if (!_values.TryGetValue(key, out var storedValue))
        {
            return null;
        }

        // Array of references case
        var referenceArray = storedValue.AsArray();
        if (referenceArray != null)
        {
            return referenceArray.GetObject(0);
        }

        // Single reference case: return the existing object from the document to keep stream data
        if (storedValue is IPdfValue<PdfReference> referenceValue)
        {
            var reference = referenceValue.Value;
            return Document.ObjectCache.GetObject(reference);
        }

        // return synthetic object
        return new PdfObject(default, Document, storedValue);
    }

    /// <summary>
    /// Gets a list of PDF objects for the specified key, handling arrays of references and single items.
    /// Returns <c>null</c> if not present or no objects found.
    /// </summary>
    /// <param name="key">The key to retrieve.</param>
    /// <returns>A list of <see cref="PdfObject"/> values, or <c>null</c>.</returns>
    public List<PdfObject> GetObjects(PdfString key)
    {
        if (!_values.TryGetValue(key, out var storedValue))
        {
            return null;
        }

        var results = new List<PdfObject>();
        var referenceArray = storedValue.ResolveToNonReference(Document)?.AsArray();

        if (referenceArray != null)
        {
            for (int i = 0; i < referenceArray.Count; i++)
            {
                var item = referenceArray.GetObject(i);

                if (item != null)
                {
                    results.Add(item);
                }
            }

            return results;
        }

        // Single item fallback
        var single = GetObject(key);
        if (single != null)
        {
            results.Add(single);
        }

        return results.Count > 0 ? results : null;
    }

    /// <summary>
    /// Sets the value for the specified key. Internal use only for parsing.
    /// </summary>
    /// <param name="key">The key to set.</param>
    /// <param name="value">The value to assign.</param>
    internal void Set(PdfString key, IPdfValue value)
    {
        _values[key] = value;
    }
}