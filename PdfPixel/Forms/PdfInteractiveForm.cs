using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Resources;

namespace PdfPixel.Forms;

/// <summary>
/// Document-level interactive form data (the catalog's AcroForm entry).
/// </summary>
public sealed class PdfInteractiveForm
{
    private PdfInteractiveForm(PdfDictionary dictionary)
    {
        PdfResourceDictionary? defaultResources = PdfResourceDictionary.GetResourceDictionary(dictionary, PdfTokens.DRKey);
        if (defaultResources != null)
        {
            DefaultResources = new PdfResourceNode(dictionary.Document, defaultResources, parent: null);
        }

        NeedAppearances = dictionary.GetBooleanOrDefault(PdfTokens.NeedAppearancesKey);
        DefaultAppearance = dictionary.GetString(PdfTokens.DefaultAppearanceKey);
        TextAlignment = (PdfTextAlignment)dictionary.GetIntegerOrDefault(PdfTokens.QuadKey);

        int signatureFlags = dictionary.GetIntegerOrDefault(PdfTokens.SigFlagsKey);
        SignatureFlags = (PdfSignatureFlags)signatureFlags & (PdfSignatureFlags.SignaturesExist | PdfSignatureFlags.AppendOnly);
    }

    /// <summary>
    /// Gets whether appearance streams must be constructed for all fields and widgets (NeedAppearances entry).
    /// </summary>
    public bool NeedAppearances { get; }

    /// <summary>
    /// Gets the document-wide default appearance string (DA entry).
    /// </summary>
    public PdfString? DefaultAppearance { get; }

    /// <summary>
    /// Gets the document-wide justification of variable text (Q entry). Default <see cref="PdfTextAlignment.Left"/>.
    /// </summary>
    public PdfTextAlignment TextAlignment { get; }

    /// <summary>
    /// Gets the document-level signature field characteristics (SigFlags entry).
    /// </summary>
    public PdfSignatureFlags SignatureFlags { get; }

    /// <summary>
    /// Gets the default resources default appearance names resolve against, or <see langword="null"/> when absent.
    /// </summary>
    internal PdfResourceNode? DefaultResources { get; }

    /// <summary>
    /// The interactive form of <paramref name="catalog"/>, or <see langword="null"/> when the catalog has none.
    /// </summary>
    internal static PdfInteractiveForm? FromCatalog(PdfDictionary catalog)
    {
        PdfDictionary? dictionary = catalog.GetDictionary(PdfTokens.AcroFormKey);
        if (dictionary == null)
        {
            return null;
        }

        return new PdfInteractiveForm(dictionary);
    }
}
