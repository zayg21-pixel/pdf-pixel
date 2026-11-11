using PdfPixel.Geometry;

namespace PdfPixel.Snap.Shading;

/// <summary>
/// Returns the parameter of a parametric shading at <paramref name="point"/> in the shading's space, or
/// <see langword="null"/> where the shading paints nothing.
/// </summary>
internal delegate float? SnapShadingParameter(in PdfPoint point);
