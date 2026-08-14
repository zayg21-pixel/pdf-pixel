using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using System;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, parent page, and original object.
/// </summary>
public sealed class PdfForm
{
    private PdfForm(
        in PdfMatrix matrix,
        in PdfRectangle bbox,
        PdfTransparencyGroup? transparencyGroup,
        IPdfPageInternal page,
        PdfObject xObject)
    {
        Matrix = matrix;
        BBox = bbox;
        TransparencyGroup = transparencyGroup;
        Page = page;
        XObject = xObject;
    }

    /// <summary>
    /// The transformation matrix (/Matrix) for the form. Identity if not specified.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// The bounding box (/BBox) for the form. Empty if not specified.
    /// </summary>
    public PdfRectangle BBox { get; }

    /// <summary>
    /// The transparency group (/Group) for the form, if present.
    /// </summary>
    public PdfTransparencyGroup? TransparencyGroup { get; }

    /// <summary>
    /// The parent page for this form.
    /// </summary>
    internal IPdfPageInternal Page { get; }

    /// <summary>
    /// The original Form XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Creates a <see cref="PdfForm"/> from a Form XObject.
    /// </summary>
    /// <param name="xObject">The Form XObject.</param>
    /// <param name="page">Parent page.</param>
    /// <returns>A parsed <see cref="PdfForm"/> instance.</returns>
    internal static PdfForm FromXObject(PdfObject xObject, IPdfPageInternal page)
    {
        PdfDictionary dict = xObject.Dictionary;
        PdfArray? matrixArray = dict.GetArray(PdfTokens.MatrixKey);
        PdfArray? bboxArray = dict.GetArray(PdfTokens.BBoxKey);
        PdfDictionary? groupDict = dict.GetDictionary(PdfTokens.GroupKey);

        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;
        PdfRectangle bbox = PdfRectangle.FromArray(bboxArray) ?? PdfRectangle.Empty;

        PdfTransparencyGroup? transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(groupDict, page);

        return new PdfForm(matrix, bbox, transparencyGroup, page, xObject);
    }

    /// <summary>
    /// Creates a <see cref="FormXObjectPageWrapper"/> for this form using the stored page and resources.
    /// </summary>
    /// <returns>A <see cref="FormXObjectPageWrapper"/> instance.</returns>
    internal FormXObjectPageWrapper GetFormPage() => new(Page, XObject);

    /// <summary>
    /// Returns the decoded form stream data as <c>ReadOnlyMemory&lt;byte&gt;</c>.
    /// </summary>
    /// <returns>The decoded form stream data.</returns>
    public ReadOnlyMemory<byte> GetFormData() => XObject.DecodeAsMemory();


    /// <summary>
    /// Gets the bounding rectangle of the object after applying the current transformation matrix.
    /// </summary>
    /// <returns>A <see cref="PdfRectangle"/> representing the transformed bounding rectangle.</returns>
    public PdfRectangle GetTransformedBounds() => Matrix.MapRect(BBox);
}
