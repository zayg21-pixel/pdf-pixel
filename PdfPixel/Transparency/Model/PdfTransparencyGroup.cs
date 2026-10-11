using PdfPixel.Color.ColorSpace;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Represents a PDF transparency group.
/// </summary>
public class PdfTransparencyGroup
{
    /// <summary>
    /// The group's blending color space (CS) as written.
    /// </summary>
    public PdfColorSpaceReference ColorSpaceReference { get; set; }

    /// <summary>
    /// Isolated flag (I). When true, the group composites against a transparent backdrop.
    /// </summary>
    public bool Isolated { get; set; }

    /// <summary>
    /// Knockout flag (K). Parsed but not yet applied during compositing.
    /// </summary>
    public bool Knockout { get; set; }

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

        if (groupDictionary.GetName(PdfTokens.GroupSubtypeKey) != PdfTokens.TransparencyGroupValue)
        {
            return null;
        }

        PdfTransparencyGroup group = new()
        {
            ColorSpaceReference = PdfColorSpaceReference.FromDictionary(groupDictionary, PdfTokens.GroupColorSpaceKey),
            Isolated = groupDictionary.GetBooleanOrDefault(PdfTokens.GroupIsolatedKey),
            Knockout = groupDictionary.GetBooleanOrDefault(PdfTokens.GroupKnockoutKey)
        };

        return group;
    }
}
