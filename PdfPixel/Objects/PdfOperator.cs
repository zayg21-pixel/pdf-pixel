namespace PdfPixel.Objects;

/// <summary>
/// Content stream operators (ISO 32000-2, Annex A), valued by their token bytes packed little-endian.
/// </summary>
public enum PdfOperator : uint
{
    /// <summary>
    /// Token longer than three bytes.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// <c>q</c>: save graphics state.
    /// </summary>
    SaveState = 'q',

    /// <summary>
    /// <c>Q</c>: restore graphics state.
    /// </summary>
    RestoreState = 'Q',

    /// <summary>
    /// <c>cm</c>: concatenate matrix to the current transformation matrix.
    /// </summary>
    ConcatMatrix = 'c' | ('m' << 8),

    /// <summary>
    /// <c>w</c>: set line width.
    /// </summary>
    LineWidth = 'w',

    /// <summary>
    /// <c>J</c>: set line cap style.
    /// </summary>
    LineCap = 'J',

    /// <summary>
    /// <c>j</c>: set line join style.
    /// </summary>
    LineJoin = 'j',

    /// <summary>
    /// <c>M</c>: set miter limit.
    /// </summary>
    MiterLimit = 'M',

    /// <summary>
    /// <c>d</c>: set line dash pattern.
    /// </summary>
    DashPattern = 'd',

    /// <summary>
    /// <c>gs</c>: set parameters from graphics state parameter dictionary.
    /// </summary>
    GraphicsStateParameters = 'g' | ('s' << 8),

    /// <summary>
    /// <c>ri</c>: set color rendering intent.
    /// </summary>
    RenderingIntent = 'r' | ('i' << 8),

    /// <summary>
    /// <c>i</c>: set flatness tolerance.
    /// </summary>
    Flatness = 'i',

    /// <summary>
    /// <c>BT</c>: begin text object.
    /// </summary>
    BeginText = 'B' | ('T' << 8),

    /// <summary>
    /// <c>ET</c>: end text object.
    /// </summary>
    EndText = 'E' | ('T' << 8),

    /// <summary>
    /// <c>Tf</c>: set text font and size.
    /// </summary>
    TextFont = 'T' | ('f' << 8),

    /// <summary>
    /// <c>Tc</c>: set character spacing.
    /// </summary>
    CharacterSpacing = 'T' | ('c' << 8),

    /// <summary>
    /// <c>Tw</c>: set word spacing.
    /// </summary>
    WordSpacing = 'T' | ('w' << 8),

    /// <summary>
    /// <c>Tz</c>: set horizontal text scaling.
    /// </summary>
    HorizontalScaling = 'T' | ('z' << 8),

    /// <summary>
    /// <c>TL</c>: set text leading.
    /// </summary>
    TextLeading = 'T' | ('L' << 8),

    /// <summary>
    /// <c>Ts</c>: set text rise.
    /// </summary>
    TextRise = 'T' | ('s' << 8),

    /// <summary>
    /// <c>Tr</c>: set text rendering mode.
    /// </summary>
    TextRenderingMode = 'T' | ('r' << 8),

    /// <summary>
    /// <c>Td</c>: move text position.
    /// </summary>
    MoveTextPosition = 'T' | ('d' << 8),

    /// <summary>
    /// <c>TD</c>: move text position and set leading.
    /// </summary>
    MoveTextPositionAndSetLeading = 'T' | ('D' << 8),

    /// <summary>
    /// <c>T*</c>: move to start of next text line.
    /// </summary>
    NextLine = 'T' | ('*' << 8),

    /// <summary>
    /// <c>Tm</c>: set text matrix and text line matrix.
    /// </summary>
    TextMatrix = 'T' | ('m' << 8),

    /// <summary>
    /// <c>Tj</c>: show text.
    /// </summary>
    ShowText = 'T' | ('j' << 8),

    /// <summary>
    /// <c>TJ</c>: show text, allowing individual glyph positioning.
    /// </summary>
    ShowTextWithPositioning = 'T' | ('J' << 8),

    /// <summary>
    /// <c>'</c>: move to next line and show text.
    /// </summary>
    ShowTextNextLine = '\'',

    /// <summary>
    /// <c>"</c>: set word and character spacing, move to next line, and show text.
    /// </summary>
    SetSpacingAndShowText = '"',

    /// <summary>
    /// <c>m</c>: begin new subpath.
    /// </summary>
    MoveTo = 'm',

    /// <summary>
    /// <c>l</c>: append straight line segment to path.
    /// </summary>
    LineTo = 'l',

    /// <summary>
    /// <c>c</c>: append curved segment to path (three control points).
    /// </summary>
    CurveTo = 'c',

    /// <summary>
    /// <c>v</c>: append curved segment to path (initial point replicated).
    /// </summary>
    CurveToV = 'v',

    /// <summary>
    /// <c>y</c>: append curved segment to path (final point replicated).
    /// </summary>
    CurveToY = 'y',

    /// <summary>
    /// <c>h</c>: close subpath.
    /// </summary>
    ClosePath = 'h',

    /// <summary>
    /// <c>re</c>: append rectangle to path.
    /// </summary>
    Rectangle = 'r' | ('e' << 8),

    /// <summary>
    /// <c>W</c>: set clipping path using nonzero winding number rule.
    /// </summary>
    Clip = 'W',

    /// <summary>
    /// <c>W*</c>: set clipping path using even-odd rule.
    /// </summary>
    ClipEvenOdd = 'W' | ('*' << 8),

    /// <summary>
    /// <c>S</c>: stroke path.
    /// </summary>
    Stroke = 'S',

    /// <summary>
    /// <c>s</c>: close and stroke path.
    /// </summary>
    CloseAndStroke = 's',

    /// <summary>
    /// <c>f</c>: fill path using nonzero winding number rule.
    /// </summary>
    Fill = 'f',

    /// <summary>
    /// <c>F</c>: fill path using nonzero winding number rule (obsolete).
    /// </summary>
    FillObsolete = 'F',

    /// <summary>
    /// <c>f*</c>: fill path using even-odd rule.
    /// </summary>
    FillEvenOdd = 'f' | ('*' << 8),

    /// <summary>
    /// <c>B</c>: fill and stroke path using nonzero winding number rule.
    /// </summary>
    FillAndStroke = 'B',

    /// <summary>
    /// <c>B*</c>: fill and stroke path using even-odd rule.
    /// </summary>
    FillAndStrokeEvenOdd = 'B' | ('*' << 8),

    /// <summary>
    /// <c>b</c>: close, fill, and stroke path using nonzero winding number rule.
    /// </summary>
    CloseFillAndStroke = 'b',

    /// <summary>
    /// <c>b*</c>: close, fill, and stroke path using even-odd rule.
    /// </summary>
    CloseFillAndStrokeEvenOdd = 'b' | ('*' << 8),

    /// <summary>
    /// <c>n</c>: end path without filling or stroking.
    /// </summary>
    EndPath = 'n',

    /// <summary>
    /// <c>g</c>: set gray level for nonstroking operations.
    /// </summary>
    FillGray = 'g',

    /// <summary>
    /// <c>rg</c>: set RGB color for nonstroking operations.
    /// </summary>
    FillRgb = 'r' | ('g' << 8),

    /// <summary>
    /// <c>k</c>: set CMYK color for nonstroking operations.
    /// </summary>
    FillCmyk = 'k',

    /// <summary>
    /// <c>G</c>: set gray level for stroking operations.
    /// </summary>
    StrokeGray = 'G',

    /// <summary>
    /// <c>RG</c>: set RGB color for stroking operations.
    /// </summary>
    StrokeRgb = 'R' | ('G' << 8),

    /// <summary>
    /// <c>K</c>: set CMYK color for stroking operations.
    /// </summary>
    StrokeCmyk = 'K',

    /// <summary>
    /// <c>cs</c>: set color space for nonstroking operations.
    /// </summary>
    FillColorSpace = 'c' | ('s' << 8),

    /// <summary>
    /// <c>CS</c>: set color space for stroking operations.
    /// </summary>
    StrokeColorSpace = 'C' | ('S' << 8),

    /// <summary>
    /// <c>sc</c>: set color for nonstroking operations.
    /// </summary>
    FillColor = 's' | ('c' << 8),

    /// <summary>
    /// <c>SC</c>: set color for stroking operations.
    /// </summary>
    StrokeColor = 'S' | ('C' << 8),

    /// <summary>
    /// <c>scn</c>: set color for nonstroking operations (ICCBased and special color spaces).
    /// </summary>
    FillColorN = 's' | ('c' << 8) | ('n' << 16),

    /// <summary>
    /// <c>SCN</c>: set color for stroking operations (ICCBased and special color spaces).
    /// </summary>
    StrokeColorN = 'S' | ('C' << 8) | ('N' << 16),

    /// <summary>
    /// <c>BI</c>: begin inline image object.
    /// </summary>
    BeginInlineImage = 'B' | ('I' << 8),

    /// <summary>
    /// <c>ID</c>: begin inline image data.
    /// </summary>
    InlineImageData = 'I' | ('D' << 8),

    /// <summary>
    /// <c>EI</c>: end inline image object.
    /// </summary>
    EndInlineImage = 'E' | ('I' << 8),

    /// <summary>
    /// <c>MP</c>: define marked-content point.
    /// </summary>
    MarkedContentPoint = 'M' | ('P' << 8),

    /// <summary>
    /// <c>DP</c>: define marked-content point with property list.
    /// </summary>
    MarkedContentPointWithProperties = 'D' | ('P' << 8),

    /// <summary>
    /// <c>BMC</c>: begin marked-content sequence.
    /// </summary>
    BeginMarkedContent = 'B' | ('M' << 8) | ('C' << 16),

    /// <summary>
    /// <c>BDC</c>: begin marked-content sequence with property list.
    /// </summary>
    BeginMarkedContentWithProperties = 'B' | ('D' << 8) | ('C' << 16),

    /// <summary>
    /// <c>EMC</c>: end marked-content sequence.
    /// </summary>
    EndMarkedContent = 'E' | ('M' << 8) | ('C' << 16),

    /// <summary>
    /// <c>Do</c>: invoke named XObject.
    /// </summary>
    InvokeXObject = 'D' | ('o' << 8),

    /// <summary>
    /// <c>BX</c>: begin compatibility section.
    /// </summary>
    BeginCompatibility = 'B' | ('X' << 8),

    /// <summary>
    /// <c>EX</c>: end compatibility section.
    /// </summary>
    EndCompatibility = 'E' | ('X' << 8),

    /// <summary>
    /// <c>d0</c>: set glyph width in Type 3 font.
    /// </summary>
    GlyphWidth = 'd' | ('0' << 8),

    /// <summary>
    /// <c>d1</c>: set glyph width and bounding box in Type 3 font.
    /// </summary>
    GlyphWidthAndBoundingBox = 'd' | ('1' << 8),

    /// <summary>
    /// <c>sh</c>: paint area defined by shading pattern.
    /// </summary>
    Shading = 's' | ('h' << 8)
}
