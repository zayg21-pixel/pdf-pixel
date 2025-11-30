using PdfReader.Models;
using PdfReader.PostScript.Tokens;
using PdfReader.Text;

namespace PdfReader.Fonts.Model;

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
    /// <param name="dictionary">The PDF dictionary containing CIDSystemInfo keys.</param>
    /// <returns>A populated <see cref="PdfCidSystemInfo"/> or null if the dictionary is null.</returns>
    public static PdfCidSystemInfo FromDictionary(PdfDictionary dictionary)
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

    /// <summary>
    /// Generates a <see cref="PdfCidSystemInfo"/> from a PostScript dictionary.
    /// </summary>
    /// <param name="dictionary">Dictionary containing CID system information.</param>
    /// <returns>A populated <see cref="PdfCidSystemInfo"/> or null if the dictionary is null.</returns>
    public static PdfCidSystemInfo FromPostscriptDictionary(PostScriptDictionary dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        var info = new PdfCidSystemInfo();

        if (dictionary.Entries.TryGetValue(PdfTokens.RegistryKey.ToString(), out var registryValue) && registryValue is PostScriptString registryString)
        {
            info.Registry = new PdfString(registryString.Value);
        }
        if (dictionary.Entries.TryGetValue(PdfTokens.OrderingKey.ToString(), out var orderingValue) && orderingValue is PostScriptString orderingString)
        {
            info.Ordering = new PdfString(orderingString.Value);
        }
        if (dictionary.Entries.TryGetValue(PdfTokens.SupplementKey.ToString(), out var supplementValue) && supplementValue is PostScriptNumber supplementInteger)
        {
            info.Supplement = (int)supplementInteger.Value;
        }

        return info;
    }
}