namespace PdfPixel.Imaging.Jpx.Model;

/// <summary>
/// JPEG2000 progression orders defining packet sequence.
/// </summary>
internal enum JpxProgressionOrder : byte
{
    /// <summary>Layer-Resolution-Component-Position</summary>
    LRCP = 0,
    /// <summary>Resolution-Layer-Component-Position</summary>
    RLCP = 1,
    /// <summary>Resolution-Position-Component-Layer</summary>
    RPCL = 2,
    /// <summary>Position-Component-Resolution-Layer</summary>
    PCRL = 3,
    /// <summary>Component-Position-Resolution-Layer</summary>
    CPRL = 4
}