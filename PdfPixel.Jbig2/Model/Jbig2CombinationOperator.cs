namespace PdfPixel.Jbig2.Model;

/// <summary>
/// JBIG2 combination (compositing) operators used for combining region bitmaps onto the page buffer.
/// Defined in ITU-T T.88 Table 5.
/// </summary>
public enum Jbig2CombinationOperator
{
    /// <summary>OR: dst = dst | src.</summary>
    Or = 0,

    /// <summary>AND: dst = dst &amp; src.</summary>
    And = 1,

    /// <summary>XOR: dst = dst ^ src.</summary>
    Xor = 2,

    /// <summary>XNOR: dst = ~(dst ^ src).</summary>
    Xnor = 3,

    /// <summary>REPLACE: dst = src.</summary>
    Replace = 4
}
