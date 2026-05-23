namespace PdfPixel.Jbig2.Model;

/// <summary>
/// JBIG2 segment types as defined in ITU-T T.88 Table 2.
/// </summary>
public enum Jbig2SegmentType
{
    /// <summary>Symbol dictionary segment (type 0).</summary>
    SymbolDictionary = 0,

    /// <summary>Intermediate text region segment (type 4).</summary>
    IntermediateTextRegion = 4,

    /// <summary>Immediate text region segment (type 6).</summary>
    ImmediateTextRegion = 6,

    /// <summary>Immediate lossless text region segment (type 7).</summary>
    ImmediateLosslessTextRegion = 7,

    /// <summary>Pattern dictionary segment (type 16).</summary>
    PatternDictionary = 16,

    /// <summary>Intermediate halftone region segment (type 20).</summary>
    IntermediateHalftoneRegion = 20,

    /// <summary>Immediate halftone region segment (type 22).</summary>
    ImmediateHalftoneRegion = 22,

    /// <summary>Immediate lossless halftone region segment (type 23).</summary>
    ImmediateLosslessHalftoneRegion = 23,

    /// <summary>Intermediate generic region segment (type 36).</summary>
    IntermediateGenericRegion = 36,

    /// <summary>Immediate generic region segment (type 38).</summary>
    ImmediateGenericRegion = 38,

    /// <summary>Immediate lossless generic region segment (type 39).</summary>
    ImmediateLosslessGenericRegion = 39,

    /// <summary>Intermediate generic refinement region segment (type 40).</summary>
    IntermediateGenericRefinementRegion = 40,

    /// <summary>Immediate generic refinement region segment (type 42).</summary>
    ImmediateGenericRefinementRegion = 42,

    /// <summary>Immediate lossless generic refinement region segment (type 43).</summary>
    ImmediateLosslessGenericRefinementRegion = 43,

    /// <summary>Page information segment (type 48).</summary>
    PageInformation = 48,

    /// <summary>End of page segment (type 49).</summary>
    EndOfPage = 49,

    /// <summary>End of stripe segment (type 50).</summary>
    EndOfStripe = 50,

    /// <summary>End of file segment (type 51).</summary>
    EndOfFile = 51,

    /// <summary>Profiles segment (type 52).</summary>
    Profiles = 52,

    /// <summary>Tables segment (type 53).</summary>
    Tables = 53,

    /// <summary>Extension segment (type 62).</summary>
    Extension = 62
}
