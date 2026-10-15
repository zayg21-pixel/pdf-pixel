using System.Collections.Generic;

namespace PdfPixel.Png.Model;

/// <summary>
/// Image header, palette, transparency, ICC profile and informational chunks of a PNG image.
/// </summary>
public sealed class PngImageInfo
{
    /// <summary>
    /// Gets or sets the image width in pixels (IHDR).
    /// </summary>
    public int Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels (IHDR).
    /// </summary>
    public int Height { get; set; }

    /// <summary>
    /// Gets or sets the bit depth of each sample, or of each palette index (IHDR).
    /// </summary>
    public int BitDepth { get; set; } = 8;

    /// <summary>
    /// Gets or sets the samples each pixel carries (IHDR).
    /// </summary>
    public PngColorType ColorType { get; set; } = PngColorType.Truecolor;

    /// <summary>
    /// Gets or sets the order the pixels are stored in (IHDR).
    /// </summary>
    public PngInterlaceMethod InterlaceMethod { get; set; } = PngInterlaceMethod.None;

    /// <summary>
    /// Gets or sets the palette as red, green and blue bytes per entry (PLTE), or null when the chunk is absent.
    /// </summary>
    public byte[]? Palette { get; set; }

    /// <summary>
    /// Gets or sets the alpha of the leading palette entries (tRNS of an indexed-color image), or null when absent.
    /// </summary>
    public byte[]? PaletteAlpha { get; set; }

    /// <summary>
    /// Gets or sets the gray sample shown fully transparent (tRNS of a grayscale image), or null when absent.
    /// </summary>
    public ushort? TransparentGray { get; set; }

    /// <summary>
    /// Gets or sets the color shown fully transparent (tRNS of a truecolor image), or null when absent.
    /// </summary>
    public PngRgbSample? TransparentColor { get; set; }

    /// <summary>
    /// Gets or sets the name of the embedded ICC profile (iCCP), or null when the chunk is absent.
    /// </summary>
    public string? IccProfileName { get; set; }

    /// <summary>
    /// Gets or sets the embedded ICC profile, decompressed (iCCP), or null when the chunk is absent.
    /// </summary>
    public byte[]? IccProfile { get; set; }

    /// <summary>
    /// Gets or sets the number of significant bits of each channel (sBIT), or null when the chunk is absent.
    /// </summary>
    public byte[]? SignificantBits { get; set; }

    /// <summary>
    /// Gets or sets the palette index of the background (bKGD of an indexed-color image), or null when absent.
    /// </summary>
    public byte? BackgroundPaletteIndex { get; set; }

    /// <summary>
    /// Gets or sets the gray sample of the background (bKGD of a grayscale image), or null when absent.
    /// </summary>
    public ushort? BackgroundGray { get; set; }

    /// <summary>
    /// Gets or sets the color of the background (bKGD of a truecolor image), or null when absent.
    /// </summary>
    public PngRgbSample? BackgroundColor { get; set; }

    /// <summary>
    /// Gets or sets the intended pixel size or aspect ratio (pHYs), or null when the chunk is absent.
    /// </summary>
    public PngPhysicalDimensions? PhysicalDimensions { get; set; }

    /// <summary>
    /// Gets or sets the Exif profile (eXIf), or null when the chunk is absent.
    /// </summary>
    public byte[]? Exif { get; set; }

    /// <summary>
    /// Gets or sets the compressed image data, or null when the image was not read from a file.
    /// </summary>
    internal List<PngDataSegment>? ImageData { get; set; }
}
