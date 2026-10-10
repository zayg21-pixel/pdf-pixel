using PdfPixel.Color;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Tagging.Model.Attributes;

/// <summary>
/// Layout attributes (/O /Layout).
/// </summary>
public sealed class PdfLayoutAttribute : PdfStructureAttributeBase
{
    internal PdfLayoutAttribute(PdfDictionary dictionary, int? revision)
        : base(PdfStructureAttributeOwner.Layout, revision)
    {
        Placement = dictionary.GetName(PdfTokens.PlacementKey)?.AsEnum<PdfStructurePlacement>();
        WritingMode = dictionary.GetName(PdfTokens.WritingModeKey)?.AsEnum<PdfStructureWritingMode>();
        BackgroundColor = ReadColor(dictionary.GetArray(PdfTokens.BackgroundColorKey));
        BorderColor = ReadColorSides(dictionary.GetArray(PdfTokens.BorderColorKey));
        BorderStyle = ReadBorderStyleSides(dictionary, PdfTokens.LayoutBorderStyleKey);
        BorderThickness = ReadNumberSides(dictionary, PdfTokens.BorderThicknessKey);
        Padding = ReadNumberSides(dictionary, PdfTokens.PaddingKey);
        Color = ReadColor(dictionary.GetArray(PdfTokens.LayoutColorKey));
        SpaceBefore = dictionary.GetFloat(PdfTokens.SpaceBeforeKey);
        SpaceAfter = dictionary.GetFloat(PdfTokens.SpaceAfterKey);
        StartIndent = dictionary.GetFloat(PdfTokens.StartIndentKey);
        EndIndent = dictionary.GetFloat(PdfTokens.EndIndentKey);
        TextIndent = dictionary.GetFloat(PdfTokens.TextIndentKey);
        TextAlign = dictionary.GetName(PdfTokens.TextAlignKey)?.AsEnum<PdfStructureTextAlign>();
        BBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey));
        Width = ReadLength(dictionary, PdfTokens.WidthKey);
        Height = ReadLength(dictionary, PdfTokens.HeightKey);
        BlockAlign = dictionary.GetName(PdfTokens.BlockAlignKey)?.AsEnum<PdfStructureBlockAlign>();
        InlineAlign = dictionary.GetName(PdfTokens.InlineAlignKey)?.AsEnum<PdfStructureInlineAlign>();
        TableBorderStyle = ReadBorderStyleSides(dictionary, PdfTokens.TableBorderStyleKey);
        TablePadding = ReadNumberSides(dictionary, PdfTokens.TablePaddingKey);
        BaselineShift = dictionary.GetFloat(PdfTokens.BaselineShiftKey);
        LineHeight = ReadLength(dictionary, PdfTokens.LineHeightKey);
        TextPosition = dictionary.GetName(PdfTokens.TextPositionKey)?.AsEnum<PdfStructureTextPosition>();
        TextDecorationColor = ReadColor(dictionary.GetArray(PdfTokens.TextDecorationColorKey));
        TextDecorationThickness = dictionary.GetFloat(PdfTokens.TextDecorationThicknessKey);
        TextDecorationType = dictionary.GetName(PdfTokens.TextDecorationTypeKey)?.AsEnum<PdfStructureTextDecorationType>();
        RubyAlign = dictionary.GetName(PdfTokens.RubyAlignKey)?.AsEnum<PdfStructureRubyAlign>();
        RubyPosition = dictionary.GetName(PdfTokens.RubyPositionKey)?.AsEnum<PdfStructureRubyPosition>();
        GlyphOrientationVertical = ReadGlyphOrientation(dictionary);
        ColumnCount = dictionary.GetInteger(PdfTokens.ColumnCountKey);
        ColumnGap = ReadNumbers(dictionary, PdfTokens.ColumnGapKey);
        ColumnWidths = ReadNumbers(dictionary, PdfTokens.ColumnWidthsKey);
    }

    /// <summary>
    /// Positioning of the element (Placement), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructurePlacement? Placement { get; }

    /// <summary>
    /// Layout progression directions (WritingMode), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureWritingMode? WritingMode { get; }

    /// <summary>
    /// Background color (BackgroundColor), or <see langword="null"/> when absent.
    /// </summary>
    public PdfColor? BackgroundColor { get; }

    /// <summary>
    /// Border color of each edge (BorderColor), or <see langword="null"/> when absent.
    /// An edge is <see langword="null"/> when it is not drawn.
    /// </summary>
    public PdfStructureSides<PdfColor?>? BorderColor { get; }

    /// <summary>
    /// Border style of each edge (BorderStyle), or <see langword="null"/> when absent.
    /// An edge is <see langword="null"/> when it is not drawn.
    /// </summary>
    public PdfStructureSides<PdfStructureBorderStyle?>? BorderStyle { get; }

    /// <summary>
    /// Border thickness of each edge (BorderThickness), or <see langword="null"/> when absent.
    /// An edge is <see langword="null"/> when it is not drawn.
    /// </summary>
    public PdfStructureSides<float?>? BorderThickness { get; }

    /// <summary>
    /// Padding of each edge (Padding), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureSides<float?>? Padding { get; }

    /// <summary>
    /// Text color (Color), or <see langword="null"/> when absent.
    /// </summary>
    public PdfColor? Color { get; }

    /// <summary>
    /// Extra space before the element (SpaceBefore), or <see langword="null"/> when absent.
    /// </summary>
    public float? SpaceBefore { get; }

    /// <summary>
    /// Extra space after the element (SpaceAfter), or <see langword="null"/> when absent.
    /// </summary>
    public float? SpaceAfter { get; }

    /// <summary>
    /// Distance from the start edge of the reference area (StartIndent), or <see langword="null"/> when absent.
    /// </summary>
    public float? StartIndent { get; }

    /// <summary>
    /// Distance from the end edge of the reference area (EndIndent), or <see langword="null"/> when absent.
    /// </summary>
    public float? EndIndent { get; }

    /// <summary>
    /// Indent of the first line of text (TextIndent), or <see langword="null"/> when absent.
    /// </summary>
    public float? TextIndent { get; }

    /// <summary>
    /// Alignment of text within lines (TextAlign), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureTextAlign? TextAlign { get; }

    /// <summary>
    /// Bounding box of the element's visible content (BBox), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? BBox { get; }

    /// <summary>
    /// Width of the content rectangle (Width), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureLength? Width { get; }

    /// <summary>
    /// Height of the content rectangle (Height), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureLength? Height { get; }

    /// <summary>
    /// Block-progression alignment within a table cell (BlockAlign), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureBlockAlign? BlockAlign { get; }

    /// <summary>
    /// Inline-progression alignment within a table cell (InlineAlign), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureInlineAlign? InlineAlign { get; }

    /// <summary>
    /// Border style of each edge of a table cell (TBorderStyle), or <see langword="null"/> when absent.
    /// An edge is <see langword="null"/> when it is not drawn.
    /// </summary>
    public PdfStructureSides<PdfStructureBorderStyle?>? TableBorderStyle { get; }

    /// <summary>
    /// Padding of each edge of a table cell (TPadding), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureSides<float?>? TablePadding { get; }

    /// <summary>
    /// Baseline shift relative to the parent element (BaselineShift), or <see langword="null"/> when absent.
    /// </summary>
    public float? BaselineShift { get; }

    /// <summary>
    /// Preferred line height (LineHeight), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureLength? LineHeight { get; }

    /// <summary>
    /// Position relative to the surrounding content (TextPosition, PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureTextPosition? TextPosition { get; }

    /// <summary>
    /// Text decoration color (TextDecorationColor), or <see langword="null"/> when absent.
    /// </summary>
    public PdfColor? TextDecorationColor { get; }

    /// <summary>
    /// Text decoration line thickness (TextDecorationThickness), or <see langword="null"/> when absent.
    /// </summary>
    public float? TextDecorationThickness { get; }

    /// <summary>
    /// Text decoration (TextDecorationType), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureTextDecorationType? TextDecorationType { get; }

    /// <summary>
    /// Justification of ruby lines (RubyAlign), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureRubyAlign? RubyAlign { get; }

    /// <summary>
    /// Placement of ruby text (RubyPosition), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureRubyPosition? RubyPosition { get; }

    /// <summary>
    /// Glyph orientation in vertical text (GlyphOrientationVertical), or <see langword="null"/> when absent.
    /// </summary>
    public PdfStructureGlyphOrientation? GlyphOrientationVertical { get; }

    /// <summary>
    /// Number of columns (ColumnCount), or <see langword="null"/> when absent.
    /// </summary>
    public int? ColumnCount { get; }

    /// <summary>
    /// Space between adjacent columns (ColumnGap), or <see langword="null"/> when absent.
    /// A single number is returned as a one-element array.
    /// </summary>
    public float[]? ColumnGap { get; }

    /// <summary>
    /// Width of each column (ColumnWidths), or <see langword="null"/> when absent.
    /// A single number is returned as a one-element array.
    /// </summary>
    public float[]? ColumnWidths { get; }

    private static PdfColor? ReadColor(PdfArray? components)
    {
        if (components == null)
        {
            return null;
        }

        float? red = components.GetFloat(0);
        float? green = components.GetFloat(1);
        float? blue = components.GetFloat(2);
        if (red == null || green == null || blue == null)
        {
            return null;
        }

        return new PdfColor(red.Value, green.Value, blue.Value);
    }

    private static PdfStructureSides<PdfColor?>? ReadColorSides(PdfArray? value)
    {
        if (value == null)
        {
            return null;
        }

        if (value.GetFloat(0) != null)
        {
            return new PdfStructureSides<PdfColor?>(ReadColor(value));
        }

        return new PdfStructureSides<PdfColor?>(
            ReadColor(value.GetArray(0)),
            ReadColor(value.GetArray(1)),
            ReadColor(value.GetArray(2)),
            ReadColor(value.GetArray(3)));
    }

    private static PdfStructureSides<PdfStructureBorderStyle?>? ReadBorderStyleSides(PdfDictionary dictionary, in PdfString key)
    {
        PdfString? style = dictionary.GetName(key);
        if (style != null)
        {
            return new PdfStructureSides<PdfStructureBorderStyle?>(style.Value.AsEnum<PdfStructureBorderStyle>());
        }

        PdfArray? styles = dictionary.GetArray(key);
        if (styles == null)
        {
            return null;
        }

        return new PdfStructureSides<PdfStructureBorderStyle?>(
            styles.GetName(0)?.AsEnum<PdfStructureBorderStyle>(),
            styles.GetName(1)?.AsEnum<PdfStructureBorderStyle>(),
            styles.GetName(2)?.AsEnum<PdfStructureBorderStyle>(),
            styles.GetName(3)?.AsEnum<PdfStructureBorderStyle>());
    }

    private static PdfStructureSides<float?>? ReadNumberSides(PdfDictionary dictionary, in PdfString key)
    {
        float? number = dictionary.GetFloat(key);
        if (number != null)
        {
            return new PdfStructureSides<float?>(number);
        }

        PdfArray? numbers = dictionary.GetArray(key);
        if (numbers == null)
        {
            return null;
        }

        return new PdfStructureSides<float?>(numbers.GetFloat(0), numbers.GetFloat(1), numbers.GetFloat(2), numbers.GetFloat(3));
    }

    private static PdfStructureLength? ReadLength(PdfDictionary dictionary, in PdfString key)
    {
        float? number = dictionary.GetFloat(key);
        if (number != null)
        {
            return new PdfStructureLength(number.Value);
        }

        PdfString? mode = dictionary.GetName(key);
        if (mode == null)
        {
            return null;
        }

        return new PdfStructureLength(mode.Value.AsEnum<PdfStructureLengthMode>());
    }

    private static PdfStructureGlyphOrientation? ReadGlyphOrientation(PdfDictionary dictionary)
    {
        int? angle = dictionary.GetInteger(PdfTokens.GlyphOrientationVerticalKey);
        if (angle != null)
        {
            return new PdfStructureGlyphOrientation(angle.Value);
        }

        PdfString? mode = dictionary.GetName(PdfTokens.GlyphOrientationVerticalKey);
        if (mode == null)
        {
            return null;
        }

        return new PdfStructureGlyphOrientation(mode.Value.AsEnum<PdfStructureGlyphOrientationMode>());
    }

    private static float[]? ReadNumbers(PdfDictionary dictionary, in PdfString key)
    {
        float? number = dictionary.GetFloat(key);
        if (number != null)
        {
            return new float[] { number.Value };
        }

        return dictionary.GetArray(key)?.GetFloatArray();
    }
}
