using PdfPixel.Jpeg.Parsing;

using System;
using System.Collections.Generic;

namespace PdfPixel.Jpeg;

/// <summary>
/// Represents parsed JPEG header metadata required for custom decoding and color handling.
/// Contains structural and metadata information from header segments, excluding entropy-coded scan data.
/// </summary>
public sealed class JpegHeader
{
    /// <summary>
    /// Gets or sets the JPEG frame type determined by the SOF marker (e.g. baseline, extended sequential, progressive).
    /// </summary>
    public JpegFrameType FrameType { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the entropy-coded data contains more than one SOS scan.
    /// Extended sequential (SOF1) images with multiple scans require dedicated multi-scan decoding.
    /// </summary>
    public bool HasMultipleScans { get; internal set; }

    /// <summary>
    /// Gets or sets the sample precision (bits per sample, typically 8).
    /// </summary>
    public byte SamplePrecision { get; internal set; }

    /// <summary>
    /// Gets or sets the image width in pixels.
    /// </summary>
    public int Width { get; internal set; }

    /// <summary>
    /// Gets or sets the image height in pixels.
    /// </summary>
    public int Height { get; internal set; }

    /// <summary>
    /// Gets or sets the number of image components (e.g., 1 = Gray, 3 = YCbCr, 4 = CMYK).
    /// </summary>
    public int ComponentCount { get; internal set; }

    /// <summary>
    /// Gets the list of image components described by the SOF segment.
    /// </summary>
    public JpegComponent[] Components { get; internal set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether Huffman tables are present (DHT segment).
    /// </summary>
    public bool HasHuffmanTables { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether quantization tables are present (DQT segment).
    /// </summary>
    public bool HasQuantizationTables { get; internal set; }

    /// <summary>
    /// Gets the list of Huffman tables parsed from DHT segments.
    /// </summary>
    public IReadOnlyList<JpegHuffmanTable> HuffmanTables { get; internal set; } = [];

    /// <summary>
    /// Gets the list of quantization tables parsed from DQT segments.
    /// </summary>
    public IReadOnlyList<JpegQuantizationTable> QuantizationTables { get; internal set; } = [];

    /// <summary>
    /// Gets or sets the restart interval (from DRI segment); 0 means no restart markers.
    /// </summary>
    public int RestartInterval { get; internal set; }

    /// <summary>
    /// Gets the list of scan specifications parsed from SOS segments.
    /// </summary>
    public IReadOnlyList<JpegScanSpec> Scans { get; internal set; } = [];

    /// <summary>
    /// Gets or sets the offset of entropy-coded data following the first SOS header.
    /// </summary>
    public int ContentOffset { get; internal set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether the APP0 (JFIF) marker is present.
    /// </summary>
    public bool IsJfif { get; internal set; }

    /// <summary>
    /// Gets or sets the JFIF version (APP0 marker).
    /// </summary>
    public ushort JfifVersion { get; internal set; }

    /// <summary>
    /// Gets or sets the density units (APP0 marker): 0 = none, 1 = dots per inch, 2 = dots per cm.
    /// </summary>
    public byte DensityUnits { get; internal set; }

    /// <summary>
    /// Gets or sets the horizontal pixel density (APP0 marker).
    /// </summary>
    public ushort XDensity { get; internal set; }

    /// <summary>
    /// Gets or sets the vertical pixel density (APP0 marker).
    /// </summary>
    public ushort YDensity { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the APP1 (Exif) marker is present.
    /// </summary>
    public bool IsExif { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the APP14 (Adobe) marker is present.
    /// </summary>
    public bool HasAdobeApp14 { get; internal set; }

    /// <summary>
    /// Gets or sets the Adobe color transform value (APP14 marker): 0 = Unknown/CMYK, 1 = YCbCr, 2 = YCCK.
    /// </summary>
    public byte AdobeColorTransform { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether an ICC profile (APP2 ICC_PROFILE) is present.
    /// </summary>
    public bool HasIccProfile { get; internal set; }

    /// <summary>
    /// Gets the list of ICC profile segments (APP2 ICC_PROFILE).
    /// </summary>
    public IReadOnlyList<JpegIccSegmentInfo> IccProfileSegments { get; internal set; } = [];

    /// <summary>
    /// Reassembles the ICC profile from <see cref="IccProfileSegments"/>.
    /// </summary>
    /// <returns>The profile bytes, or null when the segments are absent, incomplete or inconsistent.</returns>
    public byte[]? GetIccProfile() => JpegHeaderParser.AssembleIccProfile(IccProfileSegments);

    /// <summary>
    /// Parses a JPEG header from <paramref name="bytes"/>, collecting frame, component, quantization,
    /// Huffman, and APP metadata up to and including the first SOS marker.
    /// </summary>
    /// <param name="bytes">Complete JPEG byte span.</param>
    /// <returns>
    /// Populated <see cref="JpegHeader"/> with <see cref="ContentOffset"/> pointing at the entropy-coded data.
    /// </returns>
    public static JpegHeader Parse(in ReadOnlySpan<byte> bytes) => JpegHeaderParser.Parse(bytes);
}
