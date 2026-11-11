namespace PdfPixel.Snap.Rasterization;

/// <summary>
/// Receives the coverage <see cref="SnapRasterizer"/> produces, one row at a time, top to bottom.
/// </summary>
internal interface ISnapCoverageSink
{
    /// <summary>
    /// Receives one row that covers at least one pixel.
    /// </summary>
    void AddRow(in SnapCoverageRow row);
}
