namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// Describes the role of a single image component as declared in the JP2 Channel Definition box (cdef).
/// Per ISO/IEC 15444-1 Table I-11.
/// </summary>
internal sealed class JpxChannelDefinition
{
    /// <summary>
    /// Gets or sets the zero-based component index this entry describes.
    /// </summary>
    public ushort ComponentIndex { get; set; }

    /// <summary>
    /// Gets or sets the channel type.
    /// 0 = colour channel, 1 = unassociated (straight) alpha, 2 = pre-multiplied alpha,
    /// 65535 = channel not associated with any colour/alpha.
    /// </summary>
    public ushort ChannelType { get; set; }

    /// <summary>
    /// Gets or sets the association value.
    /// 0 = applies to the whole image, 1..N = specific colour channel, 65535 = no association.
    /// </summary>
    public ushort Association { get; set; }

    /// <summary>
    /// Gets a value indicating whether this component carries unassociated (straight) alpha.
    /// </summary>
    public bool IsUnassociatedAlpha => ChannelType == 1;

    /// <summary>
    /// Gets a value indicating whether this component carries pre-multiplied alpha.
    /// </summary>
    public bool IsPreMultipliedAlpha => ChannelType == 2;

    /// <summary>
    /// Gets a value indicating whether this component carries any kind of alpha (type 1 or 2).
    /// </summary>
    public bool IsAlpha => ChannelType == 1 || ChannelType == 2;
}
