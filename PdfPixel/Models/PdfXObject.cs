using PdfPixel.Files;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.OptionalContent.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF XObject.
/// </summary>
public abstract class PdfXObject
{
    /// <summary>
    /// Initializes a new <see cref="PdfXObject"/> wrapping the given source object.
    /// </summary>
    private protected PdfXObject(PdfObject xObject, in PdfReference reference, PdfXObjectSubtype subtype)
    {
        XObject = xObject;
        Reference = reference;
        Subtype = subtype;
        OptionalContent = PdfOptionalContentMembership.FromDictionary(xObject.Dictionary);
        StructParent = xObject.Dictionary.GetInteger(PdfTokens.StructParentKey);
        AssociatedFiles = PdfFileSpecification.GetFileSpecifications(xObject.Dictionary.GetArray(PdfTokens.AssociatedFilesKey));
        Metadata = PdfMetadata.FromDictionary(xObject.Dictionary);
    }

    /// <summary>
    /// Source PDF object representing the XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Reference of the XObject stream.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Subtype of the XObject.
    /// </summary>
    public PdfXObjectSubtype Subtype { get; }

    /// <summary>
    /// Optional content membership controlling visibility of this XObject.
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; }

    /// <summary>
    /// Key of the XObject's entry in the structural parent tree (/StructParent), or <see langword="null"/> when absent.
    /// </summary>
    public int? StructParent { get; }

    /// <summary>
    /// Files associated with the entire XObject (/AF, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfFileSpecification>? AssociatedFiles { get; }

    /// <summary>
    /// XObject metadata stream (/Metadata), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMetadata? Metadata { get; }

    /// <summary>
    /// Returns the XObject held by <paramref name="key"/> of <paramref name="dictionary"/>, using the document's cache if available.
    /// </summary>
    /// <param name="dictionary">Dictionary holding the XObject entry.</param>
    /// <param name="key">Key of the XObject entry.</param>
    /// <returns>The <see cref="PdfImage"/> or <see cref="PdfForm"/>, or <see langword="null"/> when absent or of an unsupported subtype.</returns>
    internal static PdfXObject? GetXObject(PdfDictionary dictionary, in PdfString key)
        => dictionary.Document.ObjectCache.GetOrCreate(dictionary, key, FromObject, requiresStream: true);

    /// <summary>
    /// Creates the XObject of the type its /Subtype names.
    /// </summary>
    /// <param name="xObject">The XObject stream object.</param>
    /// <returns>The <see cref="PdfImage"/> or <see cref="PdfForm"/>, or <see langword="null"/> for a direct object or an unsupported subtype.</returns>
    internal static PdfXObject? FromObject(PdfObject xObject)
    {
        if (xObject.Reference == null)
        {
            return null;
        }

        PdfXObjectSubtype subtype = xObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfXObjectSubtype>();

        return subtype switch
        {
            PdfXObjectSubtype.Image => new PdfImage(xObject, xObject.Reference.Value, isInlineImage: false),
            PdfXObjectSubtype.Form => new PdfForm(xObject, xObject.Reference.Value),
            _ => null
        };
    }
}
