namespace PdfPixel.Geometry.PathOperations;

/// <summary>
/// Reusable backing storage for a curve's arc-length table.
/// </summary>
internal sealed class PdfCurveLengthBuffer
{
    public float[] Distances { get; } = new float[PdfCubicContourSegment.MaxCurveMeasureSamples + 1];

    public float[] Parameters { get; } = new float[PdfCubicContourSegment.MaxCurveMeasureSamples + 1];
}
