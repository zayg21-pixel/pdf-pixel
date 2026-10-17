using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Streams;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, and content stream.
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
        PdfResourceDictionary? resources = PdfResourceDictionary.GetResourceDictionary(dictionary, PdfTokens.ResourcesKey);
        if (resources != null)
        {
            Resources = new PdfResourceNode(Document, resources, null);
        }

        StructParents = dictionary.GetInteger(PdfTokens.StructParentsKey);
        Stream = xObject.Stream;
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
    /// The form's resource dictionary (/Resources), or <see langword="null"/> when absent.
    /// </summary>
    internal PdfResourceNode? Resources { get; }

    /// <summary>
    /// Key of the form's content in the structural parent tree (/StructParents), or <see langword="null"/> when absent.
    /// </summary>
    internal int? StructParents { get; }

    /// <summary>
    /// The form's content stream.
    /// </summary>
    internal PdfObjectStream Stream { get; }

    /// <summary>
    /// Creates a <see cref="PdfContentHost"/> for this form's content stream, resolving against the form's own
    /// /Resources or, when absent, the parent content host's resources.
    /// </summary>
    /// <param name="state">Graphics state the form is drawn in.</param>
    /// <returns>A <see cref="PdfContentHost"/> instance.</returns>
    internal PdfContentHost GetFormHost(PdfGraphicsState state)
        => new(Document, Resources ?? state.Host.Resources, StructParents);

    /// <summary>
    /// Gets the bounding rectangle of the object after applying the current transformation matrix.
    /// </summary>
    /// <returns>A <see cref="PdfRectangle"/> representing the transformed bounding rectangle.</returns>
    public PdfRectangle GetTransformedBounds() => Matrix.MapRect(BBox);
}
