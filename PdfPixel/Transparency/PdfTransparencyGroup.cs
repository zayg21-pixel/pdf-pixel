using PdfPixel.Color.ColorSpace;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Transparency;

/// <summary>
/// Represents a PDF transparency group.
/// </summary>
public class PdfTransparencyGroup
{
    private PdfTransparencyGroup(in PdfColorSpaceReference colorSpaceReference, bool isolated, bool knockout)
    {
        ColorSpaceReference = colorSpaceReference;
        Isolated = isolated;
        Knockout = knockout;
    }

    /// <summary>
    /// The group's blending color space (CS) as written.
    /// </summary>
    public PdfColorSpaceReference ColorSpaceReference { get; }

    /// <summary>
    /// Isolated flag (I). When true, the group composites against a transparent backdrop.
    /// </summary>
    public bool Isolated { get; }

    /// <summary>
    /// Knockout flag (K). Parsed but not yet applied during compositing.
    /// </summary>
    public bool Knockout { get; }

    /// <summary>
    /// True when the group is isolated or knockout.
    /// </summary>
    public bool RequiresLayer => Isolated || Knockout;

    /// <summary>
    /// The transparency group <paramref name="ownerDictionary"/> holds under <paramref name="key"/>, or
    /// <see langword="null"/> when absent or not a transparency group.
    /// </summary>
    /// <param name="ownerDictionary">Dictionary holding the group entry.</param>
    /// <param name="key">Key of the group entry.</param>
    internal static PdfTransparencyGroup? GetTransparencyGroup(PdfDictionary ownerDictionary, in PdfString key)
        => ownerDictionary.Document.ObjectCache.GetOrCreate(ownerDictionary, key, FromObject);

    /// <summary>
    /// Parses a transparency group dictionary, or returns <see langword="null"/> when it is not a transparency group.
    /// </summary>
    /// <param name="groupObject">The group dictionary object.</param>
    private static PdfTransparencyGroup? FromObject(PdfObject groupObject)
    {
        PdfDictionary groupDictionary = groupObject.Dictionary;

        if (groupDictionary.GetNameOrDefault(PdfTokens.GroupSubtypeKey).AsEnum<PdfGroupSubtype>() != PdfGroupSubtype.Transparency)
        {
            return null;
        }

        return new PdfTransparencyGroup(
            PdfColorSpaceReference.FromDictionary(groupDictionary, PdfTokens.GroupColorSpaceKey),
            groupDictionary.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey),
            groupDictionary.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey));
    }
}
