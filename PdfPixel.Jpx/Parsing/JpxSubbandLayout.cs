namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Pre-computed layout for one (resolution, subbandIndex) pair within the precinct state array.
/// </summary>
internal struct JpxSubbandLayout
{
    public int BaseOffset;
    public int PrecinctsX;
    public int PrecinctsY;
    public int PrecinctStride; // = PrecinctsX * PrecinctsY
}
