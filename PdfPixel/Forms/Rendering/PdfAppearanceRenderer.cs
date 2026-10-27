using PdfPixel.Annotations;
using PdfPixel.Annotations.Rendering;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.Operators;
using PdfPixel.Rendering.Text;
using PdfPixel.Resources;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Forms.Rendering;

/// <summary>
/// Renders the primitives appearances are drawn from: background, border and text in a box, with a default appearance applied.
/// </summary>
internal static class PdfAppearanceRenderer
{
    private const float DefaultFontSize = 12f;

    private static readonly PdfString DefaultFontName = (PdfString)"Helvetica"u8;
    private static readonly string[] LineBreaks = ["\r\n", "\r", "\n"];

    /// <summary>
    /// Creates the graphics state <paramref name="defaultAppearance"/> sets, with its names resolved against the
    /// interactive form's default resources. Operators not allowed in a default appearance are skipped.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="page">The page the appearance is drawn on.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="defaultAppearance">The default appearance string, or null when absent.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    /// <param name="observer">Observer for long-running operations.</param>
    public static PdfGraphicsState CreateState(
        IPdfCommandProcessor processor,
        IPdfPageInternal page,
        PdfRenderer renderer,
        PdfString? defaultAppearance,
        PdfRenderingParameters renderingParameters,
        IPdfExecutionObserver observer)
    {
        IPdfDocumentInternal document = page.Document;
        PdfResourceNode resources = document.InteractiveForm?.DefaultResources ?? document.ContentHost.Resources;
        PdfContentHost host = new(document, resources, structuralParents: null);
        PdfGraphicsState state = new(host, page.MediaBox, new HashSet<uint>(), observer, renderingParameters);

        if (defaultAppearance == null)
        {
            return state;
        }

        PdfOperandStack operandStack = new();
        GraphicsStateOperators graphicsStateOperators = new(host, processor, operandStack, new Stack<PdfGraphicsState>());
        ColorOperators colorOperators = new(operandStack, host);
        TextOperators textOperators = new(renderer, host, processor, operandStack);

        PdfParseContext parseContext = new(defaultAppearance.Value.Value, document);
        PdfParser parser = new(parseContext, document, PdfParserMode.ContentStream);

        while (parser.TryReadContentStreamValue(out PdfContentStreamValue value))
        {
            if (value.Kind != PdfContentStreamValueKind.Operator)
            {
                operandStack.Push(value);
                continue;
            }

            PdfOperator op = value.GetOperator();
            if (IsDefaultAppearanceOperator(op))
            {
                PdfString? fontName = (op == PdfOperator.TextFont) ? GetFontName(operandStack, parseContext) : null;

                bool handled = graphicsStateOperators.ProcessOperator(op, parseContext, ref state)
                    || colorOperators.ProcessOperator(op, parseContext, ref state)
                    || textOperators.ProcessOperator(op, parseContext, ref state);

                if (handled && fontName != null && state.CurrentFont == null)
                {
                    state.CurrentFont = document.FontProvider.GetNamedFont(fontName.Value);
                }
            }

            operandStack.Clear();
        }

        return state;
    }

    /// <summary>
    /// Fills <paramref name="box"/> with <paramref name="color"/>.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="box">The box to fill.</param>
    /// <param name="color">The background color.</param>
    public static void DrawBackground(IPdfCommandProcessor processor, in PdfRectangle box, in PdfColor color)
    {
        PdfPathBuilder pathBuilder = new();
        pathBuilder.AddRect(box);

        processor.Process(new DrawPathCommand(pathBuilder.ToPath(), PdfAnnotationPaintFactory.CreateFillPaint(color)));
    }

    /// <summary>
    /// Strokes a border of <paramref name="strokeStyle"/> inside <paramref name="box"/>, with <paramref name="borderEffect"/> applied.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="box">The box the border lies inside.</param>
    /// <param name="strokeStyle">The border width and dash pattern.</param>
    /// <param name="borderEffect">The border effect.</param>
    /// <param name="color">The border color.</param>
    public static void DrawBorder(
        IPdfCommandProcessor processor,
        in PdfRectangle box,
        PdfStrokeStyle strokeStyle,
        in PdfAnnotationBorderEffect borderEffect,
        in PdfColor color)
    {
        float lineWidth = strokeStyle.LineWidth;
        if (lineWidth <= 0)
        {
            return;
        }

        PdfPathBuilder pathBuilder = new();
        pathBuilder.AddRect(box.Inflate(-lineWidth / 2f));
        PdfPath borderPath = borderEffect.Apply(pathBuilder.ToPath(), lineWidth);

        processor.Process(new DrawPathCommand(borderPath, PdfAnnotationPaintFactory.CreateStrokePaint(color, strokeStyle, borderEffect)));
    }

    /// <summary>
    /// Draws <paramref name="text"/> from the top of <paramref name="box"/> down, wrapped to its width and aligned by
    /// <paramref name="alignment"/>, in the font, size and paint of <paramref name="state"/>.
    /// </summary>
    /// <param name="processor">The command processor to emit commands to.</param>
    /// <param name="renderer">The renderer context.</param>
    /// <param name="state">The graphics state a default appearance set.</param>
    /// <param name="text">The text to draw.</param>
    /// <param name="box">The box the text is laid out in.</param>
    /// <param name="alignment">The justification of each line.</param>
    public static void DrawText(
        IPdfCommandProcessor processor,
        PdfRenderer renderer,
        PdfGraphicsState state,
        string text,
        in PdfRectangle box,
        PdfTextAlignment alignment)
    {
        PdfNamedFont font = GetNamedFont(state);
        if (state.FontSize == 0)
        {
            state.FontSize = DefaultFontSize;
        }

        float fontSize = state.FontSize;
        float horizontalScale = fontSize * state.HorizontalScaling / 100f;
        PdfFontMetrics metrics = GetMetrics(state, font);
        float lineHeight = (state.Leading > 0) ? state.Leading : (metrics.Ascent - metrics.Descent) * fontSize;

        ShapedGlyphBuffer glyphs = new();
        List<string> lines = [];
        foreach (string paragraph in text.Split(LineBreaks, StringSplitOptions.None))
        {
            AddWrappedLines(paragraph, font, state, glyphs, horizontalScale, box.Width, lines);
        }

        PdfMatrix textMatrix = state.TextMatrix;
        float baseline = box.Bottom - (metrics.Ascent * fontSize);

        foreach (string line in lines)
        {
            font.Shape(line, state, glyphs);
            float lineWidth = TextRenderUtilities.GetTextWidth(glyphs.Glyphs) * horizontalScale;
            float lineStart = alignment switch
            {
                PdfTextAlignment.Centered => box.Left + ((box.Width - lineWidth) / 2f),
                PdfTextAlignment.Right => box.Right - lineWidth,
                _ => box.Left
            };

            state.TextMatrix = new PdfMatrix(textMatrix.ScaleX, textMatrix.SkewX, lineStart, textMatrix.SkewY, textMatrix.ScaleY, baseline);
            renderer.DrawTextSequence(processor, glyphs.Glyphs, state, font);

            baseline -= lineHeight;
        }
    }

    private static PdfNamedFont GetNamedFont(PdfGraphicsState state)
    {
        if (state.CurrentFont is PdfNamedFont namedFont)
        {
            return namedFont;
        }

        PdfString baseFont = state.CurrentFont?.BaseFont ?? DefaultFontName;
        return state.Host.Document.FontProvider.GetNamedFont(baseFont);
    }

    private static PdfFontMetrics GetMetrics(PdfGraphicsState state, PdfNamedFont font)
    {
        IPdfTypeface? typeface = font.Typeface;
        if (typeface != null)
        {
            return typeface.Metrics;
        }

        return state.Host.Document.FontProvider.GetFallbackTypeface(font.SubstitutionInfo).Metrics;
    }

    private static void AddWrappedLines(
        string paragraph,
        PdfNamedFont font,
        PdfGraphicsState state,
        ShapedGlyphBuffer glyphs,
        float horizontalScale,
        float maximumWidth,
        List<string> lines)
    {
        string[] words = paragraph.Split(' ');
        string currentLine = words[0];

        for (int wordIndex = 1; wordIndex < words.Length; wordIndex++)
        {
            string candidate = currentLine + " " + words[wordIndex];
            font.Shape(candidate, state, glyphs);

            if (TextRenderUtilities.GetTextWidth(glyphs.Glyphs) * horizontalScale <= maximumWidth)
            {
                currentLine = candidate;
            }
            else
            {
                lines.Add(currentLine);
                currentLine = words[wordIndex];
            }
        }

        lines.Add(currentLine);
    }

    private static PdfString? GetFontName(PdfOperandStack operandStack, PdfParseContext parseContext)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = operandStack.AsSpan();
        if (operands.Length < 2)
        {
            return null;
        }

        return operands[operands.Length - 2].GetPdfValue(parseContext).AsName();
    }

    private static bool IsDefaultAppearanceOperator(PdfOperator op)
    {
        switch (op)
        {
            case PdfOperator.LineWidth:
            case PdfOperator.LineCap:
            case PdfOperator.LineJoin:
            case PdfOperator.MiterLimit:
            case PdfOperator.DashPattern:
            case PdfOperator.RenderingIntent:
            case PdfOperator.Flatness:
            case PdfOperator.GraphicsStateParameters:
            case PdfOperator.StrokeColorSpace:
            case PdfOperator.FillColorSpace:
            case PdfOperator.StrokeColor:
            case PdfOperator.StrokeColorN:
            case PdfOperator.FillColor:
            case PdfOperator.FillColorN:
            case PdfOperator.StrokeGray:
            case PdfOperator.FillGray:
            case PdfOperator.StrokeRgb:
            case PdfOperator.FillRgb:
            case PdfOperator.StrokeCmyk:
            case PdfOperator.FillCmyk:
            case PdfOperator.CharacterSpacing:
            case PdfOperator.WordSpacing:
            case PdfOperator.HorizontalScaling:
            case PdfOperator.TextLeading:
            case PdfOperator.TextFont:
            case PdfOperator.TextRenderingMode:
            case PdfOperator.TextRise:
            case PdfOperator.TextMatrix:
                return true;

            default:
                return false;
        }
    }
}
