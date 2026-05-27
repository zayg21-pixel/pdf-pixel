using PdfPixel.Text;
using System;

namespace PdfPixel.Models;

/// <summary>
/// Represents a PDF XObject.
/// </summary>
public class PdfXObject
{
    public PdfXObject(PdfObject xObject, PdfXObjectSubtype subtype)
    {
        XObject = xObject;
        Subtype = subtype;
    }

    /// <summary>
    /// Source PDF object representing the XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Subtype of the XObject.
    /// </summary>
    public PdfXObjectSubtype Subtype { get; }

    public static PdfXObject FromObject(PdfObject sourceObject)
    {
        if (sourceObject == null)
        {
            throw new ArgumentNullException(nameof(sourceObject));
        }

        PdfXObjectSubtype subtype = sourceObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfXObjectSubtype>();
        return new PdfXObject(sourceObject, subtype);
    }
}
