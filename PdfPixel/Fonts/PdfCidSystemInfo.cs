using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Fonts;

/// <summary>
/// Represents the CIDSystemInfo dictionary for a PDF font, describing the character collection registry, ordering, and supplement.
/// </summary>
public class PdfCidSystemInfo
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCidSystemInfo"/> class.
    /// </summary>
    /// <param name="registry">The registry name, or <see langword="null"/> when absent.</param>
    /// <param name="ordering">The ordering name, or <see langword="null"/> when absent.</param>
    /// <param name="supplement">The supplement number.</param>
    internal PdfCidSystemInfo(PdfString? registry, PdfString? ordering, int supplement)
    {
        Registry = registry;
        Ordering = ordering;
        Supplement = supplement;
    }

    /// <summary>
    /// Reads the CIDSystemInfo <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="dictionary">The CIDSystemInfo dictionary.</param>
    internal PdfCidSystemInfo(PdfDictionary dictionary)
        : this(
            dictionary.GetString(PdfTokens.RegistryKey),
            dictionary.GetString(PdfTokens.OrderingKey),
            dictionary.GetIntegerOrDefault(PdfTokens.SupplementKey))
    {
    }

    /// <summary>
    /// The registry name identifying the character collection (e.g., "Adobe").
    /// </summary>
    public PdfString? Registry { get; }

    /// <summary>
    /// The ordering name specifying the character collection (e.g., "GB1", "CNS1").
    /// </summary>
    public PdfString? Ordering { get; }

    /// <summary>
    /// The supplement number indicating the version of the character collection.
    /// </summary>
    public int Supplement { get; }
}
