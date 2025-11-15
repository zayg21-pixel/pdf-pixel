using System.Collections.Generic;

namespace PdfReader.Models;

public class PdfDictionary
{
    private readonly Dictionary<PdfString, IPdfValue> _values;

    public PdfDictionary(PdfDocument document) : this(document, null)
    {
        Document = document;
    }

    public PdfDictionary(PdfDocument document, Dictionary<PdfString, IPdfValue> values)
    {
        Document = document;
        _values = values ?? new Dictionary<PdfString, IPdfValue>();
    }

    public int Count => _values.Count;

    internal Dictionary<PdfString, IPdfValue> RawValues => _values;

    public PdfDocument Document { get; }

    public bool HasKey(PdfString key)
    {
        return _values.ContainsKey(key);
    }

    public PdfString GetName(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsName() ?? default : default;

    public PdfString GetString(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsString() ?? default : default;

    public bool GetBooleanOrDefault(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
            return resolvedValue != null && resolvedValue.AsBool();
        }
        return false;
    }

    public int? GetInteger(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);

            if (resolvedValue != null && !IsCollectionType(resolvedValue))
            {
                return resolvedValue.AsInteger();
            }
        }

        return default;
    }

    public float? GetFloat(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
            if (resolvedValue != null && !IsCollectionType(resolvedValue))
            {
                return resolvedValue.AsFloat();
            }
        }

        return default;
    }

    public bool? GetBoolean(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            var resolvedValue = storedValue.ResolveToNonReference(Document);
            if (resolvedValue != null && !IsCollectionType(resolvedValue))
            {
                return resolvedValue.AsBool();
            }
        }

        return default;
    }

    public int GetIntegerOrDefault(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsInteger() ?? 0 : 0;


    public float GetFloatOrDefault(PdfString key)
        => _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsFloat() ?? 0 : 0;

    private bool IsCollectionType(IPdfValue value)
    {
        return value.Type == PdfValueType.Array || value.Type == PdfValueType.Dictionary;
    }

    public IPdfValue GetValue(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            return storedValue.ResolveToNonReference(Document);
        }

        return null;
    }

    public PdfArray GetArray(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue))
        {
            return storedValue.ResolveToNonReference(Document).AsArray();
        }

        return null;
    }

    public PdfDictionary GetDictionary(PdfString key) =>
        _values.TryGetValue(key, out var storedValue) ? storedValue.ResolveToNonReference(Document)?.AsDictionary() : null;

    public PdfReference GetReference(PdfString key)
    {
        if (_values.TryGetValue(key, out var storedValue) && storedValue is IPdfValue<PdfReference> reference)
        {
            return reference.Value;
        }
        return default;
    }

    public PdfObject GetObject(PdfString key)
    {
        if (!_values.TryGetValue(key, out var storedValue))
            return null;

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
            return Document.GetObject(reference);
        }

        // return synthetic object
        return new PdfObject(default, Document, storedValue);
    }

    public List<PdfObject> GetObjects(PdfString key)
    {
        if (!_values.TryGetValue(key, out var storedValue))
            return null;

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

    // Internal methods for parsing
    internal void Set(PdfString key, IPdfValue value)
    {
        _values[key] = value;
    }
}