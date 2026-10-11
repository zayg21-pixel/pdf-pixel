namespace PdfPixel.Color.Icc.Model;

/// <summary>
/// ICC tag type an <see cref="IccLutPipeline"/> was parsed from.
/// </summary>
public enum IccLutPipelineType
{
    /// <summary>
    /// Legacy lut8 / lut16 (mft1 / mft2) pipeline.
    /// </summary>
    Lut = 0,

    /// <summary>
    /// Multi-process element A2B (mAB) pipeline.
    /// </summary>
    Mab = 1,

    /// <summary>
    /// Multi-process element B2A (mBA) pipeline.
    /// </summary>
    Mba = 2
}
