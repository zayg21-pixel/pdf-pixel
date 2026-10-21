namespace PdfPixel.Tagging;

/// <summary>
/// Type of a nested content stream enclosed by a <see cref="PdfContentStreamScope"/>.
/// </summary>
public enum PdfContentStreamType
{
    /// <summary>
    /// Content stream of a form XObject.
    /// </summary>
    Form,

    /// <summary>
    /// Glyph description of a Type 3 font (CharProcs).
    /// </summary>
    Type3Glyph,

    /// <summary>
    /// Transparency group of a soft mask (G).
    /// </summary>
    SoftMask
}
