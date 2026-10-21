using PdfPixel.Geometry;
using PdfPixel.Measures;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Resources;
using PdfPixel.Streams;
using PdfPixel.Transparency;
using System.Collections.Generic;

namespace PdfPixel.Forms;

/// <summary>
/// Represents a parsed PDF Form XObject with geometry, resources, transparency group, and content stream.
/// </summary>
public sealed class PdfForm : PdfXObject
{
    private readonly PdfDictionary _dictionary;
    private List<PdfPointData>? _pointData;

    internal PdfForm(PdfObject xObject, in PdfReference reference)
        : base(xObject, reference, PdfXObjectSubtype.Form)
    {
        PdfDictionary dictionary = xObject.Dictionary;
        _dictionary = dictionary;
        Matrix = PdfMatrix.FromArray(dictionary.GetArray(PdfTokens.MatrixKey)) ?? PdfMatrix.Identity;
        BoundingBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey)) ?? PdfRectangle.Empty;
        TransparencyGroup = PdfTransparencyGroup.GetTransparencyGroup(dictionary, PdfTokens.GroupKey);
        PdfResourceDictionary? resources = PdfResourceDictionary.GetResourceDictionary(dictionary, PdfTokens.ResourcesKey);
        if (resources != null)
        {
            Resources = new PdfResourceNode(Document, resources, null);
        }

        StructParents = dictionary.GetInteger(PdfTokens.StructParentsKey);
        Measure = PdfMeasure.GetMeasure(dictionary, PdfTokens.MeasureKey);
        Stream = xObject.Stream;
    }

    /// <summary>
    /// The transformation matrix (/Matrix) for the form. Identity if not specified.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// The bounding box (/BBox) for the form. Empty if not specified.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

    /// <summary>
    /// The transparency group (/Group) for the form, if present.
    /// </summary>
    public PdfTransparencyGroup? TransparencyGroup { get; }

    /// <summary>
    /// Scale and units that apply to the form (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

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
    /// Returns the extended geospatial data of the form (/PtData). Empty when absent.
    /// </summary>
    public IReadOnlyList<PdfPointData> GetPointData()
    {
        if (_pointData == null)
        {
            _pointData = PdfPointData.FromOwner(_dictionary);
        }

        return _pointData;
    }

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
    public PdfRectangle GetTransformedBounds() => Matrix.MapRect(BoundingBox);
}
