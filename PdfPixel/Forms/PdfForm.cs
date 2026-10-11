using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, and original object.
/// </summary>
public sealed class PdfForm
{
    private PdfForm(
        in PdfMatrix matrix,
        in PdfRectangle bbox,
        PdfTransparencyGroup? transparencyGroup,
        PdfObject xObject,
        in PdfReference reference)
    {
        Matrix = matrix;
        BBox = bbox;
        TransparencyGroup = transparencyGroup;
        XObject = xObject;
        Reference = reference;
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
    /// The original Form XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Reference of the Form XObject.
    /// </summary>
    public PdfReference Reference { get; }

    /// <summary>
    /// Creates a <see cref="PdfForm"/> from a Form XObject.
    /// </summary>
    /// <param name="xObject">The Form XObject.</param>
    /// <returns>A parsed <see cref="PdfForm"/> instance, or <see langword="null"/> when <paramref name="xObject"/> is a direct object.</returns>
    internal static PdfForm? FromXObject(PdfObject xObject)
    {
        if (xObject.Reference == null)
        {
            return null;
        }

        PdfDictionary dict = xObject.Dictionary;
        PdfArray? matrixArray = dict.GetArray(PdfTokens.MatrixKey);
        PdfArray? bboxArray = dict.GetArray(PdfTokens.BBoxKey);

        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;
        PdfRectangle bbox = PdfRectangle.FromArray(bboxArray) ?? PdfRectangle.Empty;

        PdfTransparencyGroup? transparencyGroup = PdfTransparencyGroup.GetTransparencyGroup(dict, PdfTokens.GroupKey);

        return new PdfForm(matrix, bbox, transparencyGroup, xObject, xObject.Reference.Value);
    }

    /// <summary>
    /// Creates a <see cref="PdfContentHost"/> for this form's content stream, resolving against the form's own
    /// /Resources or, when absent, the parent content host's resources.
    /// </summary>
    /// <param name="state">Graphics state the form is drawn in.</param>
    /// <returns>A <see cref="PdfContentHost"/> instance.</returns>
    internal PdfContentHost GetFormHost(PdfGraphicsState state)
    {
        PdfDictionary resources = XObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey) ?? state.Host.ResourceDictionary;
        int? structParents = XObject.Dictionary.GetInteger(PdfTokens.StructParentsKey);
        return new PdfContentHost(XObject.Document, resources, structParents);
    }



    /// <summary>
    /// Gets the bounding rectangle of the object after applying the current transformation matrix.
    /// </summary>
    /// <returns>A <see cref="PdfRectangle"/> representing the transformed bounding rectangle.</returns>
    public PdfRectangle GetTransformedBounds() => Matrix.MapRect(BBox);
}
