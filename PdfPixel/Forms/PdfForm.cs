using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, parent content host, and original object.
/// </summary>
public sealed class PdfForm
{
    private PdfForm(
        in PdfMatrix matrix,
        in PdfRectangle bbox,
        PdfTransparencyGroup? transparencyGroup,
        IPdfContentHost host,
        PdfObject xObject)
    {
        Matrix = matrix;
        BBox = bbox;
        TransparencyGroup = transparencyGroup;
        Host = host;
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
    /// The parent content host for this form.
    /// </summary>
    internal IPdfContentHost Host { get; }

    /// <summary>
    /// The original Form XObject.
    /// </summary>
    public PdfObject XObject { get; }

    /// <summary>
    /// Creates a <see cref="PdfForm"/> from a Form XObject.
    /// </summary>
    /// <param name="xObject">The Form XObject.</param>
    /// <param name="host">Parent content host.</param>
    /// <returns>A parsed <see cref="PdfForm"/> instance.</returns>
    internal static PdfForm FromXObject(PdfObject xObject, IPdfContentHost host)
    {
        PdfDictionary dict = xObject.Dictionary;
        PdfArray? matrixArray = dict.GetArray(PdfTokens.MatrixKey);
        PdfArray? bboxArray = dict.GetArray(PdfTokens.BBoxKey);

        PdfMatrix matrix = PdfMatrix.FromArray(matrixArray) ?? PdfMatrix.Identity;
        PdfRectangle bbox = PdfRectangle.FromArray(bboxArray) ?? PdfRectangle.Empty;

        PdfTransparencyGroup? transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(dict, PdfTokens.GroupKey, host);

        return new PdfForm(matrix, bbox, transparencyGroup, host, xObject);
    }

    /// <summary>
    /// Creates a <see cref="PdfContentHost"/> for this form's content stream, resolving against the form's own
    /// /Resources or, when absent, the parent content host's resources.
    /// </summary>
    /// <returns>A <see cref="PdfContentHost"/> instance.</returns>
    internal PdfContentHost GetFormHost()
    {
        PdfDictionary resources = XObject.Dictionary.GetDictionary(PdfTokens.ResourcesKey) ?? Host.ResourceDictionary;
        int? structParents = XObject.Dictionary.GetInteger(PdfTokens.StructParentsKey);
        return new PdfContentHost(Host.Document, resources, structParents);
    }



    /// <summary>
    /// Gets the bounding rectangle of the object after applying the current transformation matrix.
    /// </summary>
    /// <returns>A <see cref="PdfRectangle"/> representing the transformed bounding rectangle.</returns>
    public PdfRectangle GetTransformedBounds() => Matrix.MapRect(BBox);
}
