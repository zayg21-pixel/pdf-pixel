using PdfReader.Imaging.Jpg.Huffman;
using System.Collections.Generic;

namespace PdfReader.Imaging.Jpg.Model;

/// <summary>
/// Represents parsed JPEG header metadata required for custom decoding and color handling.
/// Contains structural and metadata information from header segments, excluding entropy-coded scan data.
/// </summary>
internal sealed class JpgHeader
{
    /// <summary>
    /// Gets or sets a value indicating whether the JPEG uses baseline (non-progressive) encoding.
    /// </summary>
    public bool IsBaseline { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the JPEG uses progressive encoding.
    /// </summary>
    public bool IsProgressive { get; set; }

    /// <summary>
    /// Gets or sets the sample precision (bits per sample, typically 8).
    /// </summary>
    public byte SamplePrecision { get; set; }

    /// <summary>
    /// Gets or sets the image width in pixels.
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels.
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the number of image components (e.g., 1 = Gray, 3 = YCbCr, 4 = CMYK).
    /// </summary>
    public int ComponentCount { get; set; }

    /// <summary>
    /// Gets the list of image components described by the SOF segment.
    /// </summary>
    public List<JpgComponent> Components { get; } = new List<JpgComponent>();

    /// <summary>
    /// Gets or sets a value indicating whether Huffman tables are present (DHT segment).
    /// </summary>
    public bool HasHuffmanTables { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether quantization tables are present (DQT segment).
    /// </summary>
    public bool HasQuantizationTables { get; set; }

    /// <summary>
    /// Gets the list of Huffman tables parsed from DHT segments.
    /// </summary>
    public List<JpgHuffmanTable> HuffmanTables { get; } = new List<JpgHuffmanTable>();

    /// <summary>
    /// Gets the list of quantization tables parsed from DQT segments.
    /// </summary>
    public List<JpgQuantizationTable> QuantizationTables { get; } = new List<JpgQuantizationTable>();

    /// <summary>
    /// Gets or sets the restart interval (from DRI segment); 0 means no restart markers.
    /// </summary>
    public int RestartInterval { get; set; }

    /// <summary>
    /// Gets the list of scan specifications parsed from SOS segments.
    /// </summary>
    public List<JpgScanSpec> Scans { get; } = new List<JpgScanSpec>();

    /// <summary>
    /// Gets or sets the offset of entropy-coded data following the first SOS header.
    /// </summary>
    public int ContentOffset { get; set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether the APP0 (JFIF) marker is present.
    /// </summary>
    public bool IsJfif { get; set; }

    /// <summary>
    /// Gets or sets the JFIF version (APP0 marker).
    /// </summary>
    public ushort JfifVersion { get; set; }

    /// <summary>
    /// Gets or sets the density units (APP0 marker): 0 = none, 1 = dots per inch, 2 = dots per cm.
    /// </summary>
    public byte DensityUnits { get; set; }

    /// <summary>
    /// Gets or sets the horizontal pixel density (APP0 marker).
    /// </summary>
    public ushort XDensity { get; set; }

    /// <summary>
    /// Gets or sets the vertical pixel density (APP0 marker).
    /// </summary>
    public ushort YDensity { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the APP1 (Exif) marker is present.
    /// </summary>
    public bool IsExif { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the APP14 (Adobe) marker is present.
    /// </summary>
    public bool HasAdobeApp14 { get; set; }

    /// <summary>
    /// Gets or sets the Adobe color transform value (APP14 marker): 0 = Unknown/CMYK, 1 = YCbCr, 2 = YCCK.
    /// </summary>
    public byte AdobeColorTransform { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an ICC profile (APP2 ICC_PROFILE) is present.
    /// </summary>
    public bool HasIccProfile { get; set; }

    /// <summary>
    /// Gets the list of ICC profile segments (APP2 ICC_PROFILE).
    /// </summary>
    public List<IccSegmentInfo> IccProfileSegments { get; } = new List<IccSegmentInfo>();
}
