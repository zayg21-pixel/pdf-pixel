using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Fonts.Types;

/// <summary>
/// Represents the CIDSystemInfo dictionary for a PDF font, describing the character collection registry, ordering, and supplement.
/// </summary>
/// <remarks>
/// This class is used to parse and store the CID system information required for composite (CID) fonts in PDF documents.
/// </remarks>
public class PdfCidSystemInfo
{
    /// <summary>
    /// The registry name identifying the character collection (e.g., "Adobe").
    /// </summary>
    public PdfString Registry { get; private set; }

    /// <summary>
    /// The ordering name specifying the character collection (e.g., "GB1", "CNS1").
    /// </summary>
    public PdfString Ordering { get; private set; }

    /// <summary>
    /// The supplement number indicating the version of the character collection.
    /// </summary>
    public int Supplement { get; private set; }

    /// <summary>
    /// Creates a <see cref="PdfCidSystemInfo"/> instance from a PDF dictionary.
    /// </summary>
    /// <param name="dict">The PDF dictionary containing CIDSystemInfo keys.</param>
    /// <returns>A populated <see cref="PdfCidSystemInfo"/> or null if the dictionary is null.</returns>
    public static PdfCidSystemInfo FromDictionary(PdfDictionary dict)
    {
        if (dict == null)
        {
            return null;
        }

        return new PdfCidSystemInfo
        {
            Registry = dict.GetString(PdfTokens.RegistryKey),
            Ordering = dict.GetString(PdfTokens.OrderingKey),
            Supplement = dict.GetIntegerOrDefault(PdfTokens.SupplementKey)
        };
    }
}