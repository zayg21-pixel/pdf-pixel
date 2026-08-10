using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles text-related PDF operators.
/// Converted to instance implementation for consistency with other operator processors.
/// </summary>
internal class TextOperators : IOperatorProcessor
{
    private readonly List<ShapedGlyph> buffer = [];

    private static readonly HashSet<string> SupportedOperators = [
        // Text object operators
        "BT",
        "ET",
        // Text state operators
        "Tf",
        "Tc",
        "Tw",
        "Tz",
        "TL",
        "Ts",
        "Tr",
        // Text positioning operators
        "Td",
        "TD",
        "T*",
        "Tm",
        // Text showing operators
        "Tj",
        "TJ",
        "'",
        "\""
    ];

    private readonly IPdfRenderer _renderer;
    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly ILogger<TextOperators> _logger;

    public TextOperators(IPdfRenderer renderer, IPdfPageInternal page, IPdfCommandProcessor processor, Stack<IPdfValue> operandStack)
    {
        _renderer = renderer;
        _page = page;
        _processor = processor;
        _operandStack = operandStack;
        _logger = page.Document.LoggerFactory.CreateLogger<TextOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "BT":
                {
                    ProcessBeginText(graphicsState);
                    break;
                }
            case "ET":
                {
                    ProcessEndText(graphicsState);
                    break;
                }
            case "Tf":
                {
                    ProcessSetFont(graphicsState);
                    break;
                }
            case "Tc":
                {
                    ProcessSetCharacterSpacing(graphicsState);
                    break;
                }
            case "Tw":
                {
                    ProcessSetWordSpacing(graphicsState);
                    break;
                }
            case "Tz":
                {
                    ProcessSetHorizontalScaling(graphicsState);
                    break;
                }
            case "TL":
                {
                    ProcessSetTextLeading(graphicsState);
                    break;
                }
            case "Ts":
                {
                    ProcessSetTextRise(graphicsState);
                    break;
                }
            case "Tr":
                {
                    ProcessSetTextRenderingMode(graphicsState);
                    break;
                }
            case "Td":
                {
                    ProcessMoveTextPosition(graphicsState);
                    break;
                }
            case "TD":
                {
                    ProcessMoveTextPositionAndSetLeading(graphicsState);
                    break;
                }
            case "T*":
                {
                    ProcessNextLine(graphicsState);
                    break;
                }
            case "Tm":
                {
                    ProcessSetTextMatrix(graphicsState);
                    break;
                }
            case "Tj":
                {
                    ProcessShowText(graphicsState);
                    break;
                }
            case "'":
                {
                    ProcessShowTextNextLine(graphicsState);
                    break;
                }
            case "TJ":
                {
                    ProcessShowTextWithPositioning(graphicsState);
                    break;
                }
            case "\"":
                {
                    ProcessSetSpacingAndShowText(graphicsState);
                    break;
                }
        }
    }

    private void ProcessBeginText(PdfGraphicsState graphicsState)
    {
        graphicsState.TextClipPath = null;

        graphicsState.TextMatrix = PdfMatrix.Identity;
        graphicsState.TextLineMatrix = PdfMatrix.Identity;
    }

    private void ProcessEndText(PdfGraphicsState graphicsState)
    {
        graphicsState.TextMatrix = PdfMatrix.Identity;
        graphicsState.TextLineMatrix = PdfMatrix.Identity;

        if (graphicsState.TextClipPath != null)
        {
            PdfPath textClipPath = graphicsState.TextClipPath.ToPath();
            _processor.Process(new ClipPathCommand(textClipPath, PdfClipOperation.Intersect));
            graphicsState.IntersectClipBounds(textClipPath.GetBounds());
            graphicsState.TextClipPath = null;
        }
    }

    private void ProcessSetFont(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        PdfString? fontName = operands[0].AsName();
        float fontSize = operands[1].AsFloat();
        if (fontName == null)
        {
            return;
        }

        graphicsState.CurrentFont = _page.Cache.GetFont(fontName.Value);
        graphicsState.FontSize = fontSize;
    }

    private void ProcessSetCharacterSpacing(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.CharacterSpacing = operands[0].AsFloat();
    }

    private void ProcessSetWordSpacing(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.WordSpacing = operands[0].AsFloat();
    }

    private void ProcessSetHorizontalScaling(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.HorizontalScaling = operands[0].AsFloat();
    }

    private void ProcessSetTextLeading(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.Leading = -operands[0].AsFloat();
    }

    private void ProcessSetTextRise(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.Rise = operands[0].AsFloat();
    }

    private void ProcessSetTextRenderingMode(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        var mode = (int)operands[0].AsFloat();
        if (mode >= 0 && mode <= 7)
        {
            graphicsState.TextRenderingMode = (PdfTextRenderingMode)mode;
        }
        else
        {
            graphicsState.TextRenderingMode = PdfTextRenderingMode.Fill;
        }
    }

    private void ProcessMoveTextPosition(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        float tx = operands[0].AsFloat();
        float ty = operands[1].AsFloat();
        PdfMatrix translation = PdfMatrix.CreateTranslation(tx, ty);
        graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
        graphicsState.TextMatrix = graphicsState.TextLineMatrix;
    }

    private void ProcessMoveTextPositionAndSetLeading(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        float tx = operands[0].AsFloat();
        float ty = operands[1].AsFloat();
        graphicsState.Leading = ty;
        PdfMatrix translation = PdfMatrix.CreateTranslation(tx, ty);
        graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
        graphicsState.TextMatrix = graphicsState.TextLineMatrix;
    }

    private void ProcessShowText(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        ShapedGlyphBuilder.BuildFromString(operands[0], graphicsState, buffer);
        ProcessSequence(graphicsState, buffer);
    }

    private void ProcessShowTextNextLine(PdfGraphicsState graphicsState)
    {
        ProcessNextLine(graphicsState);
        ProcessShowText(graphicsState);
    }

    private void ProcessShowTextWithPositioning(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        ShapedGlyphBuilder.BuildFromArray(operands[0], graphicsState, buffer);
        ProcessSequence(graphicsState, buffer);
    }

    private void ProcessNextLine(PdfGraphicsState graphicsState)
    {
        PdfMatrix translation = PdfMatrix.CreateTranslation(0, graphicsState.Leading);
        graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
        graphicsState.TextMatrix = graphicsState.TextLineMatrix;
    }

    private void ProcessSetTextMatrix(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
        if (operands.Count < 6)
        {
            return;
        }

        PdfMatrix matrix = PdfMatrix.FromOperands(operands) ?? PdfMatrix.Identity;
        graphicsState.TextMatrix = matrix;
        graphicsState.TextLineMatrix = matrix;
    }

    private void ProcessSetSpacingAndShowText(PdfGraphicsState graphicsState)
    {
        ProcessNextLine(graphicsState);

        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(3, _operandStack);
        if (operands.Count < 3)
        {
            return;
        }

        graphicsState.WordSpacing = operands[0].AsFloat();
        graphicsState.CharacterSpacing = operands[1].AsFloat();
        ShapedGlyphBuilder.BuildFromString(operands[2], graphicsState, buffer);
        ProcessSequence(graphicsState, buffer);
    }

    private void ProcessSequence(PdfGraphicsState graphicsState, List<ShapedGlyph> glyphs)
    {
        if (graphicsState.CurrentFont == null)
        {
            _logger.LogWarning("Skipping text show operator: no current font is set.");
            return;
        }

        PdfSize advancement = _renderer.DrawTextSequence(_processor, glyphs.ToArray(), graphicsState, graphicsState.CurrentFont);
        PdfMatrix advanceMatrix = PdfMatrix.CreateTranslation(advancement.Width, advancement.Height);
        graphicsState.TextMatrix = PdfMatrix.Concat(graphicsState.TextMatrix, advanceMatrix);
    }
}
