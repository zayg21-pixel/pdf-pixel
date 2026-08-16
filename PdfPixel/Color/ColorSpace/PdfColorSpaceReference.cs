using PdfPixel.Models;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// A colour space as written at the place it is used: a reference to the object holding it, or — when
/// written in place — the name or array itself, so that nothing it points at is resolved until a
/// converter is actually asked for.
/// </summary>
internal readonly struct PdfColorSpaceReference
{
    private PdfColorSpaceReference(in PdfReference reference, IPdfValue? value)
    {
        Reference = reference;
        Value = value;
    }

    /// <summary>
    /// Reference to the object holding the colour space. Invalid unless the colour space is indirect.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// The colour space written in place, a name or an array. Null unless it is written in place.
    /// </summary>
    public IPdfValue? Value { get; }

    /// <summary>
    /// True when a colour space was written at all.
    /// </summary>
    public bool IsPresent => Reference.IsValid || Value != null;

    /// <summary>
    /// Reads the colour space a dictionary holds under a key, resolving nothing it points at.
    /// </summary>
    public static PdfColorSpaceReference FromDictionary(PdfDictionary dictionary, in PdfString key)
    {
        if (dictionary == null)
        {
            return default;
        }

        PdfReference? reference = dictionary.GetReference(key);
        if (reference != null)
        {
            return new PdfColorSpaceReference(reference.Value, null);
        }

        if (dictionary.RawValues.TryGetValue(key, out IPdfValue? storedValue))
        {
            return new PdfColorSpaceReference(default, storedValue);
        }

        return default;
    }
}
