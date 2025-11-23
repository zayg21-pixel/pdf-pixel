using System.Collections.Generic;

namespace PdfReader.Color.Icc.Model;

/// <summary>
/// Minimal ICC profile object model exposing only the subset of metadata required for PDF color handling.
/// This type intentionally omits full transform logic; higher-level converter classes perform color space
/// conversion using the parsed data (matrices, TRCs, LUT pipelines, etc.).
/// </summary>
internal sealed partial class IccProfile
{
    /// <summary>
    /// Original ICC profile bytes.
    /// </summary>
    public byte[] Bytes { get; set; }

    /// <summary>
    /// Full ICC profile header (fixed 128-byte structure) containing high-level metadata such as
    /// device class, data color space, PCS, version, rendering intent hint and illuminant.
    /// </summary>
    public IccProfileHeader Header { get; set; }

    /// <summary>
    /// Raw tag directory entries as parsed from the profile. Maintained mainly for diagnostics
    /// or future extension if additional tag types need to be interrogated.
    /// </summary>
    public IReadOnlyList<IccTagEntry> Tags { get; set; }

    /// <summary>
    /// White point (wtpt tag) in PCS (typically D50) if present.
    /// </summary>
    public IccXyz? WhitePoint { get; set; }

    /// <summary>
    /// Black point (bkpt tag) if present (rare in many profiles).
    /// </summary>
    public IccXyz? BlackPoint { get; set; }

    /// <summary>
    /// Red channel XYZ value (rXYZ) for matrix/TRC RGB profiles.
    /// </summary>
    public IccXyz? RedMatrix { get; set; }

    /// <summary>
    /// Green channel XYZ value (gXYZ) for matrix/TRC RGB profiles.
    /// </summary>
    public IccXyz? GreenMatrix { get; set; }

    /// <summary>
    /// Blue channel XYZ value (bXYZ) for matrix/TRC RGB profiles.
    /// </summary>
    public IccXyz? BlueMatrix { get; set; }

    /// <summary>
    /// Red channel tone reproduction curve (rTRC) – may be gamma, sampled or parametric.
    /// </summary>
    public IccTrc RedTrc { get; set; }

    /// <summary>
    /// Green channel tone reproduction curve (gTRC) – may be gamma, sampled or parametric.
    /// </summary>
    public IccTrc GreenTrc { get; set; }

    /// <summary>
    /// Blue channel tone reproduction curve (bTRC) – may be gamma, sampled or parametric.
    /// </summary>
    public IccTrc BlueTrc { get; set; }

    /// <summary>
    /// Gray tone reproduction curve (kTRC) for grayscale profiles.
    /// </summary>
    public IccTrc GrayTrc { get; set; }

    /// <summary>
    /// Chromatic adaptation (Bradford) matrix (chad tag) if supplied by the profile.
    /// </summary>
    public float[,] ChromaticAdaptation { get; set; }

    /// <summary>
    /// Human-readable profile description (desc or first mluc record).
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// A2B0 (Perceptual) legacy / v2 AToB LUT structural metadata (if present).
    /// </summary>
    public IccLutAToB A2B0 { get; set; }

    /// <summary>
    /// A2B1 (Relative Colorimetric) legacy / v2 AToB LUT structural metadata (if present).
    /// </summary>
    public IccLutAToB A2B1 { get; set; }

    /// <summary>
    /// A2B2 (Saturation) legacy / v2 AToB LUT structural metadata (if present).
    /// </summary>
    public IccLutAToB A2B2 { get; set; }

    /// <summary>
    /// B2A0 (Perceptual) legacy / v2 BToA LUT structural metadata (if present).
    /// </summary>
    public IccLutBToA B2A0 { get; set; }

    /// <summary>
    /// B2A1 (Relative Colorimetric) legacy / v2 BToA LUT structural metadata (if present).
    /// </summary>
    public IccLutBToA B2A1 { get; set; }

    /// <summary>
    /// B2A2 (Saturation) legacy / v2 BToA LUT structural metadata (if present).
    /// </summary>
    public IccLutBToA B2A2 { get; set; }

    /// <summary>
    /// Fast flag indicating presence of a perceptual A2B transform.
    /// </summary>
    public bool HasA2B0 { get; set; }

    /// <summary>
    /// Fast flag indicating presence of a perceptual B2A transform.
    /// </summary>
    public bool HasB2A0 { get; set; }

    /// <summary>
    /// Parsed A2B LUT pipeline (Perceptual intent) if available – contains raw tables and CLUT.
    /// </summary>
    public IccLutPipeline A2BLut0 { get; set; }

    /// <summary>
    /// Parsed A2B LUT pipeline (Relative Colorimetric intent) if available.
    /// </summary>
    public IccLutPipeline A2BLut1 { get; set; }

    /// <summary>
    /// Parsed A2B LUT pipeline (Saturation intent) if available.
    /// </summary>
    public IccLutPipeline A2BLut2 { get; set; }
}
