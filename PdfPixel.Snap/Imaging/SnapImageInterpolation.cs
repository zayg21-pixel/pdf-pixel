namespace PdfPixel.Snap.Imaging;

/// <summary>
/// How an image source is interpolated where it is enlarged.
/// </summary>
internal enum SnapImageInterpolation
{
    /// <summary>
    /// Linearly when the image asks for interpolation, otherwise not at all.
    /// </summary>
    AsRequested,

    /// <summary>
    /// By area, whatever the image asks.
    /// </summary>
    ByArea,

    /// <summary>
    /// Never, whatever the image asks.
    /// </summary>
    Never
}
