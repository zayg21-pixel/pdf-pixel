using System;
using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Text;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles text-related PDF operators.
/// Converted to instance implementation for consistency with other operator processors.
/// </summary>
internal class TextOperators : IOperatorProcessor
{
    private readonly ShapedGlyphBuffer _glyphBuffer = new();

    private readonly IPdfRenderer _renderer;
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfOperandStack _operandStack;
    private readonly ILogger<TextOperators> _logger;

    public TextOperators(IPdfRenderer renderer, IPdfContentHost host, IPdfCommandProcessor processor, PdfOperandStack operandStack)
    {
        _renderer = renderer;
        _host = host;
        _processor = processor;
        _operandStack = operandStack;
        _logger = host.Document.LoggerFactory.CreateLogger<TextOperators>();
    }

    public bool ProcessOperator(PdfOperator op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.BeginText:
                {
                    ProcessBeginText(graphicsState);
                    break;
                }
            case PdfOperator.EndText:
                {
                    ProcessEndText(graphicsState);
                    break;
                }
            case PdfOperator.TextFont:
                {
                    ProcessSetFont(graphicsState);
                    break;
                }
            case PdfOperator.CharacterSpacing:
                {
                    ProcessSetCharacterSpacing(graphicsState);
                    break;
                }
            case PdfOperator.WordSpacing:
                {
                    ProcessSetWordSpacing(graphicsState);
                    break;
                }
            case PdfOperator.HorizontalScaling:
                {
                    ProcessSetHorizontalScaling(graphicsState);
                    break;
                }
            case PdfOperator.TextLeading:
                {
                    ProcessSetTextLeading(graphicsState);
                    break;
                }
            case PdfOperator.TextRise:
                {
                    ProcessSetTextRise(graphicsState);
                    break;
                }
            case PdfOperator.TextRenderingMode:
                {
                    ProcessSetTextRenderingMode(graphicsState);
                    break;
                }
            case PdfOperator.MoveTextPosition:
                {
                    ProcessMoveTextPosition(graphicsState);
                    break;
                }
            case PdfOperator.MoveTextPositionAndSetLeading:
                {
                    ProcessMoveTextPositionAndSetLeading(graphicsState);
                    break;
                }
            case PdfOperator.NextLine:
                {
                    ProcessNextLine(graphicsState);
                    break;
                }
            case PdfOperator.TextMatrix:
                {
                    ProcessSetTextMatrix(graphicsState);
                    break;
                }
            case PdfOperator.ShowText:
                {
                    ProcessShowText(graphicsState);
                    break;
                }
            case PdfOperator.ShowTextNextLine:
                {
                    ProcessShowTextNextLine(graphicsState);
                    break;
                }
            case PdfOperator.ShowTextWithPositioning:
                {
                    ProcessShowTextWithPositioning(graphicsState);
                    break;
                }
            case PdfOperator.SetSpacingAndShowText:
                {
                    ProcessSetSpacingAndShowText(graphicsState);
                    break;
                }
            default:
                {
                    return false;
                }
        }

        return true;
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
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

        graphicsState.CurrentFont = _host.Resources.GetFont(fontName.Value);
        graphicsState.FontSize = fontSize.Value;
    }

    private void ProcessSetCharacterSpacing(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
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
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        ShapedGlyphBuilder.BuildFromString(operands[0], graphicsState, _glyphBuffer);
        ProcessSequence(graphicsState, _glyphBuffer);
    }

    private void ProcessShowTextNextLine(PdfGraphicsState graphicsState)
    {
        ProcessNextLine(graphicsState);
        ProcessShowText(graphicsState);
    }

    private void ProcessShowTextWithPositioning(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        ShapedGlyphBuilder.BuildFromArray(operands[0], graphicsState, _glyphBuffer);
        ProcessSequence(graphicsState, _glyphBuffer);
    }

    private void ProcessNextLine(PdfGraphicsState graphicsState)
    {
        PdfMatrix translation = PdfMatrix.CreateTranslation(0, graphicsState.Leading);
        graphicsState.TextLineMatrix = translation.PostConcat(graphicsState.TextLineMatrix);
        graphicsState.TextMatrix = graphicsState.TextLineMatrix;
    }

    private void ProcessSetTextMatrix(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(6);
        if (operands.Length < 6)
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

        ReadOnlySpan<IPdfValue> operands = _operandStack.Pop(3);
        if (operands.Length < 3)
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
        ShapedGlyphBuilder.BuildFromString(operands[2], graphicsState, _glyphBuffer);
        ProcessSequence(graphicsState, _glyphBuffer);
    }

    private void ProcessSequence(PdfGraphicsState graphicsState, ShapedGlyphBuffer glyphs)
    {
        if (graphicsState.CurrentFont == null)
        {
            _logger.LogWarning("Skipping text show operator: no current font is set.");
            return;
        }

        PdfSize advancement = _renderer.DrawTextSequence(_processor, glyphs.Glyphs, graphicsState, graphicsState.CurrentFont);
        PdfMatrix advanceMatrix = PdfMatrix.CreateTranslation(advancement.Width, advancement.Height);
        graphicsState.TextMatrix = PdfMatrix.Concat(graphicsState.TextMatrix, advanceMatrix);
    }
}
