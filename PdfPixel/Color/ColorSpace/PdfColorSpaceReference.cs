using PdfPixel.Objects;
using System;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// A color space as written at the place it is used: a reference to the object holding it, or — when
/// written in place — the name or array itself, so that nothing it points at is resolved until a
/// converter is actually asked for.
/// </summary>
public readonly struct PdfColorSpaceReference
{
    private PdfColorSpaceReference(PdfReference? reference, PdfValue? value)
    {
        Reference = reference;
        Value = value;
    }

    /// <summary>
    /// Reference to the object holding the color space. Null unless the color space is indirect.
    /// </summary>
    public PdfReference? Reference { get; }

    /// <summary>
    /// The color space written in place, a name or an array. Null unless it is written in place.
    /// </summary>
    public PdfValue? Value { get; }

    /// <summary>
    /// True when a color space was written at all.
    /// </summary>
    public bool IsPresent => Reference != null || Value != null;

    /// <summary>
    /// Creates a reference to the color space <paramref name="value"/> describes directly, such as a device color space name.
    /// </summary>
    internal static PdfColorSpaceReference FromValue(PdfValue value) => new(null, value ?? throw new ArgumentNullException(nameof(value)));

    /// <summary>
    /// Reads the color space a dictionary holds under a key, resolving nothing it points at.
    /// </summary>
    internal static PdfColorSpaceReference FromDictionary(PdfDictionary? dictionary, in PdfString key)
    {
        if (dictionary == null)
        {
            return default;
        }

        PdfReference? reference = dictionary.GetReference(key);
        if (reference != null)
        {
            return new PdfColorSpaceReference(reference, null);
        }

        if (dictionary.RawValues.TryGetValue(key, out PdfValue? storedValue))
        {
            return new PdfColorSpaceReference(null, storedValue);
        }

        return default;
    }
}
