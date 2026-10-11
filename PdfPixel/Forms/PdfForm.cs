using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, and original object.
/// </summary>
public sealed class PdfForm : PdfXObject
{
    internal PdfForm(PdfObject xObject, in PdfReference reference)
        : base(xObject, reference, PdfXObjectSubtype.Form)
    {
        PdfDictionary dictionary = xObject.Dictionary;
        Matrix = PdfMatrix.FromArray(dictionary.GetArray(PdfTokens.MatrixKey)) ?? PdfMatrix.Identity;
        BBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey)) ?? PdfRectangle.Empty;
        TransparencyGroup = PdfTransparencyGroup.GetTransparencyGroup(dictionary, PdfTokens.GroupKey);
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
