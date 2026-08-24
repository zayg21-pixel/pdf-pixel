using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Represents the CIDSystemInfo dictionary for a PDF font, describing the character collection registry, ordering, and supplement.
/// </summary>
public class PdfCidSystemInfo
{
    /// <summary>
    /// The registry name identifying the character collection (e.g., "Adobe").
    /// </summary>
    public PdfString? Registry { get; internal set; }

    /// <summary>
    /// The ordering name specifying the character collection (e.g., "GB1", "CNS1").
    /// </summary>
    public PdfString? Ordering { get; internal set; }

    /// <summary>
    /// The supplement number indicating the version of the character collection.
    /// </summary>
    public int Supplement { get; internal set; }

    /// <summary>
    /// Creates a <see cref="PdfCidSystemInfo"/> instance from a PDF dictionary.
    /// </summary>
    /// <param name="dictionary">The PDF dictionary containing CIDSystemInfo keys.</param>
    /// <returns>A populated <see cref="PdfCidSystemInfo"/> or null if the dictionary is null.</returns>
    public static PdfCidSystemInfo? FromDictionary(PdfDictionary? dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        return new PdfCidSystemInfo
        {
            Registry = dictionary.GetString(PdfTokens.RegistryKey),
            Ordering = dictionary.GetString(PdfTokens.OrderingKey),
            Supplement = dictionary.GetIntegerOrDefault(PdfTokens.SupplementKey)
        };
    }

}
