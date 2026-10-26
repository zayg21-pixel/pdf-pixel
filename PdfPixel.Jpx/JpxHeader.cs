using System.Collections.Generic;

namespace PdfPixel.Jpx;

/// <summary>
/// Represents parsed JPEG 2000 (JPX) header metadata required for decoding and color handling.
/// Contains structural and metadata information from main and tile-part headers.
/// </summary>
public sealed partial class JpxHeader
{
    /// <summary>
    /// Gets or sets the image width in pixels (from SIZ marker segment).
    /// </summary>
    public uint Width { get; internal set; }

    /// <summary>
    /// Gets or sets the image height in pixels (from SIZ marker segment).
    /// </summary>
    public uint Height { get; internal set; }

    /// <summary>
    /// Gets or sets the number of image components (from SIZ marker segment).
    /// </summary>
    public ushort ComponentCount { get; internal set; }

    /// <summary>
    /// Gets the list of image components described in the SIZ segment.
    /// </summary>
    public List<JpxComponent> Components { get; } = [];

    /// <summary>
    /// Gets or sets the reference grid origin X offset (XOsiz from SIZ marker segment).
    /// </summary>
    public uint OriginX { get; internal set; }

    /// <summary>
    /// Gets or sets the reference grid origin Y offset (YOsiz from SIZ marker segment).
    /// </summary>
    public uint OriginY { get; internal set; }

    /// <summary>
    /// Gets or sets the reference tile width (XTsiz from SIZ marker segment).
    /// </summary>
    public uint TileWidth { get; internal set; }

    /// <summary>
    /// Gets or sets the reference tile height (YTsiz from SIZ marker segment).
    /// </summary>
    public uint TileHeight { get; internal set; }

    /// <summary>
    /// Gets or sets the tile origin X offset (XTOsiz from SIZ marker segment).
    /// </summary>
    public uint TileOriginX { get; internal set; }

    /// <summary>
    /// Gets or sets the tile origin Y offset (YTOsiz from SIZ marker segment).
    /// </summary>
    public uint TileOriginY { get; internal set; }

    /// <summary>
    /// Gets or sets the JPEG 2000 profile/capabilities (Rsiz from SIZ marker segment).
    /// </summary>
    public ushort Profile { get; internal set; }

    /// <summary>
    /// Gets or sets the coding style parameters from the main header COD segment.
    /// </summary>
    public JpxCodingStyle? CodingStyle { get; internal set; }

    /// <summary>
    /// Gets or sets the quantization parameters from the main header QCD segment.
    /// </summary>
    public JpxQuantization? Quantization { get; internal set; }

    /// <summary>
    /// Gets the list of component coding style overrides (COC marker segments).
    /// </summary>
    public List<JpxComponentCodingStyle> ComponentCodingStyles { get; } = [];

    /// <summary>
    /// Gets the list of component quantization overrides (QCC marker segments).
    /// </summary>
    public List<JpxComponentQuantization> ComponentQuantizations { get; } = [];

    /// <summary>
    /// Gets the quantization a component is coded with: the QCC override the codestream carries
    /// for it when there is one, and the main header's QCD otherwise.
    /// </summary>
    /// <param name="componentIndex">Zero-based index of the component.</param>
    public JpxQuantization? GetComponentQuantization(int componentIndex)
    {
        foreach (JpxComponentQuantization componentQuantization in ComponentQuantizations)
        {
            if (componentQuantization.ComponentIndex == componentIndex)
            {
                return componentQuantization.Quantization;
            }
        }

        return Quantization;
    }

    /// <summary>
    /// Gets or sets a value indicating whether the main header contains comments (COM marker segments).
    /// </summary>
    public bool HasComments { get; internal set; }

    /// <summary>
    /// Gets the list of comments from COM marker segments.
    /// </summary>
    public List<JpxComment> Comments { get; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether color specification boxes are present.
    /// </summary>
    public bool HasColorSpecification { get; internal set; }

    /// <summary>
    /// Gets the list of color specifications from colr boxes.
    /// </summary>
    public List<JpxColorSpecification> ColorSpecifications { get; } = [];

    /// <summary>
    /// Gets the channel definitions declared in the cdef box, if present.
    /// Each entry describes the role (color / alpha) of one component.
    /// </summary>
    public List<JpxChannelDefinition> ChannelDefinitions { get; } = [];

    /// <summary>
    /// Gets the zero-based index of the first opacity component declared in the cdef box,
    /// or <c>-1</c> when no opacity component is present.
    /// </summary>
    public int OpacityComponentIndex
    {
        get
        {
            foreach (JpxChannelDefinition def in ChannelDefinitions)
            {
                if (def.IsAlpha)
                {
                    return def.ComponentIndex;
                }
            }

            return -1;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the JP2 header declares at least one opacity component
    /// via the cdef box.
    /// </summary>
    public bool HasOpacityChannel => OpacityComponentIndex >= 0;

    /// <summary>
    /// Gets or sets the offset to the first tile-part header (start of actual codestream data).
    /// </summary>
    public int CodestreamOffset { get; internal set; } = -1;

    /// <summary>
    /// Gets or sets a value indicating whether this is a raw codestream (without JP2 wrapper).
    /// </summary>
    public bool IsRawCodestream { get; internal set; }

    /// <summary>
    /// Gets or sets the file type brand from the ftyp box (e.g., "jp2 ").
    /// </summary>
    public string? Brand { get; internal set; }

    /// <summary>
    /// Gets or sets the minor version from the ftyp box.
    /// </summary>
    public uint MinorVersion { get; internal set; }

    /// <summary>
    /// Gets the list of compatible brands from the ftyp box.
    /// </summary>
    public List<string> CompatibleBrands { get; } = [];
}
