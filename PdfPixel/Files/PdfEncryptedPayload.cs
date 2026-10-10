using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Files;

/// <summary>
/// Encrypted payload (/EncryptedPayload, PDF 2.0) referenced by a file specification.
/// </summary>
public sealed class PdfEncryptedPayload
{
    private PdfEncryptedPayload(in PdfString subtype, PdfString? version)
    {
        Subtype = subtype;
        Version = version;
    }

    /// <summary>
    /// Name of the cryptographic filter encrypting the payload (Subtype).
    /// </summary>
    public PdfString Subtype { get; }

    /// <summary>
    /// Version of the cryptographic filter (Version), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Version { get; }

    /// <summary>
    /// An encrypted payload over <paramref name="dictionary"/>, or <see langword="null"/> when it names no
    /// cryptographic filter (Subtype).
    /// </summary>
    internal static PdfEncryptedPayload? FromDictionary(PdfDictionary? dictionary)
    {
        PdfString? subtype = dictionary?.GetName(PdfTokens.SubtypeKey);
        if (dictionary == null || subtype == null)
        {
            return null;
        }

        return new PdfEncryptedPayload(subtype.Value, dictionary.GetName(PdfTokens.VersionKey));
    }
}
