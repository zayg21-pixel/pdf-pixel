using System.Collections.Generic;

namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Represents parsed JPEG 2000 (JPX) header metadata required for decoding and color handling.
/// Contains structural and metadata information from main and tile-part headers.
/// </summary>
internal sealed class JpxHeader
{
    /// <summary>
    /// Gets or sets the image width in pixels (from SIZ marker segment).
    /// </summary>
    public uint Width { get; set; }

    /// <summary>
    /// Gets or sets the image height in pixels (from SIZ marker segment).
    /// </summary>
    public uint Height { get; set; }

    /// <summary>
    /// Gets or sets the number of image components (from SIZ marker segment).
    /// </summary>
    public ushort ComponentCount { get; set; }

    /// <summary>
    /// Gets the list of image components described in the SIZ segment.
    /// </summary>
    public List<JpxComponent> Components { get; } = new List<JpxComponent>();

    /// <summary>
    /// Gets or sets the reference grid origin X offset (XOsiz from SIZ marker segment).
    /// </summary>
    public uint OriginX { get; set; }

    /// <summary>
    /// Gets or sets the reference grid origin Y offset (YOsiz from SIZ marker segment).
    /// </summary>
    public uint OriginY { get; set; }

    /// <summary>
    /// Gets or sets the reference tile width (XTsiz from SIZ marker segment).
    /// </summary>
    public uint TileWidth { get; set; }

    /// <summary>
    /// Gets or sets the reference tile height (YTsiz from SIZ marker segment).
    /// </summary>
    public uint TileHeight { get; set; }

    /// <summary>
    /// Gets or sets the tile origin X offset (XTOsiz from SIZ marker segment).
    /// </summary>
    public uint TileOriginX { get; set; }

    /// <summary>
    /// Gets or sets the tile origin Y offset (YTOsiz from SIZ marker segment).
    /// </summary>
    public uint TileOriginY { get; set; }

    /// <summary>
    /// Gets or sets the JPEG 2000 profile/capabilities (Rsiz from SIZ marker segment).
    /// </summary>
    public ushort Profile { get; set; }

    /// <summary>
    /// Gets or sets the coding style parameters from the main header COD segment.
    /// </summary>
    public JpxCodingStyle CodingStyle { get; set; }

    /// <summary>
    /// Gets or sets the quantization parameters from the main header QCD segment.
    /// </summary>
    public JpxQuantization Quantization { get; set; }

    /// <summary>
    /// Gets the list of component coding style overrides (COC marker segments).
    /// </summary>
    public List<JpxComponentCodingStyle> ComponentCodingStyles { get; } = new List<JpxComponentCodingStyle>();

    /// <summary>
    /// Gets the list of component quantization overrides (QCC marker segments).
    /// </summary>
    public List<JpxComponentQuantization> ComponentQuantizations { get; } = new List<JpxComponentQuantization>();

    /// <summary>
    /// Gets or sets a value indicating whether the main header contains comments (COM marker segments).
    /// </summary>
    public bool HasComments { get; set; }

    /// <summary>
    /// Gets the list of comments from COM marker segments.
    /// </summary>
    public List<JpxComment> Comments { get; } = new List<JpxComment>();

    /// <summary>
    /// Gets or sets a value indicating whether color specification boxes are present.
    /// </summary>
    public bool HasColorSpecification { get; set; }

    /// <summary>
    /// Gets the list of color specifications from colr boxes.
    /// </summary>
    public List<JpxColorSpecification> ColorSpecifications { get; } = new List<JpxColorSpecification>();

    /// <summary>
    /// Gets or sets the offset to the first tile-part header (start of actual codestream data).
    /// </summary>
    public int CodestreamOffset { get; set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether this is a raw codestream (without JP2 wrapper).
    /// </summary>
    public bool IsRawCodestream { get; set; }

    /// <summary>
    /// Gets or sets the file type brand from the ftyp box (e.g., "jp2 ").
    /// </summary>
    public string Brand { get; set; }

    /// <summary>
    /// Gets or sets the minor version from the ftyp box.
    /// </summary>
    public uint MinorVersion { get; set; }

    /// <summary>
    /// Gets the list of compatible brands from the ftyp box.
    /// </summary>
    public List<string> CompatibleBrands { get; } = new List<string>();
}