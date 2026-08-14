using PdfPixel.Text;
using System;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF XObject.
/// </summary>
public class PdfXObject
{
    /// <summary>
    /// Initializes a new <see cref="PdfXObject"/> wrapping the given source object and resolved subtype.
    /// </summary>
    public PdfXObject(PdfObject xObject, PdfXObjectSubtype subtype, PdfOptionalContentMembership? optionalContent)
    {
        XObject = xObject;
        Subtype = subtype;
        OptionalContent = optionalContent;
    }

    /// <summary>
    /// Source PDF object representing the XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Subtype of the XObject.
    /// </summary>
    public PdfXObjectSubtype Subtype { get; }

    /// <summary>
    /// Optional content membership controlling visibility of this XObject.
    /// </summary>
    public PdfOptionalContentMembership? OptionalContent { get; }

    /// <summary>
    /// Creates a <see cref="PdfXObject"/> from a PDF object by reading its /Subtype entry.
    /// </summary>
    public static PdfXObject FromObject(PdfObject sourceObject)
    {
        if (sourceObject == null)
        {
            throw new ArgumentNullException(nameof(sourceObject));
        }

        PdfXObjectSubtype subtype = sourceObject.Dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfXObjectSubtype>();
        PdfOptionalContentMembership? optionalContent = PdfOptionalContentMembership.FromDictionary(sourceObject.Dictionary);

        return new PdfXObject(sourceObject, subtype, optionalContent);
    }
}
