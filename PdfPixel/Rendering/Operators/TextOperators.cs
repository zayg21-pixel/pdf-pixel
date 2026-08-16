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
        float? fontSize = operands[1].AsFloat();
        if (fontName == null || fontSize == null)
        {
            _logger.LogWarning("Skipping 'Tf' operator: expected a font name and a numeric size.");
            return;
        }

        graphicsState.CurrentFont = _page.Cache.GetFont(fontName.Value);
        graphicsState.FontSize = fontSize.Value;
    }

    private void ProcessSetCharacterSpacing(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float? characterSpacing = operands[0].AsFloat();
        if (characterSpacing == null)
        {
            _logger.LogWarning("Skipping 'Tc' operator: non-numeric character spacing operand.");
            return;
        }

        graphicsState.CharacterSpacing = characterSpacing.Value;
    }

    private void ProcessSetWordSpacing(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float? wordSpacing = operands[0].AsFloat();
        if (wordSpacing == null)
        {
            _logger.LogWarning("Skipping 'Tw' operator: non-numeric word spacing operand.");
            return;
        }

        graphicsState.WordSpacing = wordSpacing.Value;
    }

    private void ProcessSetHorizontalScaling(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float? horizontalScaling = operands[0].AsFloat();
        if (horizontalScaling == null)
        {
            _logger.LogWarning("Skipping 'Tz' operator: non-numeric horizontal scaling operand.");
            return;
        }

        graphicsState.HorizontalScaling = horizontalScaling.Value;
    }

    private void ProcessSetTextLeading(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float? leading = operands[0].AsFloat();
        if (leading == null)
        {
            _logger.LogWarning("Skipping 'TL' operator: non-numeric leading operand.");
            return;
        }

        graphicsState.Leading = -leading.Value;
    }

    private void ProcessSetTextRise(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float? rise = operands[0].AsFloat();
        if (rise == null)
        {
            _logger.LogWarning("Skipping 'Ts' operator: non-numeric text rise operand.");
            return;
        }

        graphicsState.Rise = rise.Value;
    }

    private void ProcessSetTextRenderingMode(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        int? mode = operands[0].AsInteger();
        if (mode == null)
        {
            _logger.LogWarning("Skipping 'Tr' operator: non-numeric rendering mode operand.");
            return;
        }

        if (mode >= 0 && mode <= 7)
        {
            graphicsState.TextRenderingMode = (PdfTextRenderingMode)mode.Value;
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

        float? tx = operands[0].AsFloat();
        float? ty = operands[1].AsFloat();

        if (tx == null || ty == null)
        {
            _logger.LogWarning("Skipping 'Td' operator: non-numeric translation operands.");
            return;
        }

        PdfMatrix translation = PdfMatrix.CreateTranslation(tx.Value, ty.Value);
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

        float? tx = operands[0].AsFloat();
        float? ty = operands[1].AsFloat();

        if (tx == null || ty == null)
        {
            _logger.LogWarning("Skipping 'TD' operator: non-numeric translation operands.");
            return;
        }

        graphicsState.Leading = ty.Value;
        PdfMatrix translation = PdfMatrix.CreateTranslation(tx.Value, ty.Value);
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

        float? wordSpacing = operands[0].AsFloat();
        float? characterSpacing = operands[1].AsFloat();

        if (wordSpacing == null || characterSpacing == null)
        {
            _logger.LogWarning("Skipping '\"' operator: non-numeric word or character spacing operands.");
            return;
        }

        graphicsState.WordSpacing = wordSpacing.Value;
        graphicsState.CharacterSpacing = characterSpacing.Value;
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
