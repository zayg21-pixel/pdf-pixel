using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

internal class GraphicsStateOperators : IOperatorProcessor
{
    private readonly IPdfContentHost _host;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfOperandStack _operandStack;
    private readonly Stack<PdfGraphicsState> _graphicsStack;
    private readonly Stack<PdfGraphicsState> _freeStates = [];
    private readonly ILogger<GraphicsStateOperators> _logger;

    public GraphicsStateOperators(IPdfContentHost host, IPdfCommandProcessor processor, PdfOperandStack operandStack, Stack<PdfGraphicsState> graphicsStack)
    {
        _host = host;
        _processor = processor;
        _operandStack = operandStack;
        _graphicsStack = graphicsStack;
        _logger = host.Document.LoggerFactory.CreateLogger<GraphicsStateOperators>();
    }

    public bool ProcessOperator(PdfOperator op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.SaveState:
            {
                // Save graphics state
                ProcessSaveGraphicsState(graphicsState);
                break;
            }
            case PdfOperator.RestoreState:
            {
                // Restore graphics state
                ProcessRestoreGraphicsState(ref graphicsState);
                break;
            }
            case PdfOperator.ConcatMatrix:
            {
                // Concatenate matrix
                ProcessConcatenateMatrix(graphicsState);
                break;
            }
            case PdfOperator.LineWidth:
            {
                // Set line width
                ProcessSetLineWidth(graphicsState);
                break;
            }
            case PdfOperator.LineCap:
            {
                // Set line cap style
                ProcessSetLineCap(graphicsState);
                break;
            }
            case PdfOperator.LineJoin:
            {
                // Set line join style
                ProcessSetLineJoin(graphicsState);
                break;
            }
            case PdfOperator.MiterLimit:
            {
                // Set miter limit
                ProcessSetMiterLimit(graphicsState);
                break;
            }
            case PdfOperator.DashPattern:
            {
                // Set dash pattern
                ProcessSetDashPattern(graphicsState);
                break;
            }
            case PdfOperator.GraphicsStateParameters:
            {
                // Set graphics state parameters from ExtGState
                ProcessSetGraphicsStateParameters(graphicsState);
                break;
            }
            case PdfOperator.RenderingIntent:
            {
                // Set rendering intent
                ProcessSetRenderingIntent(graphicsState);
                break;
            }
            case PdfOperator.Flatness:
            {
                // Set flatness tolerance
                ProcessSetFlatnessTolerance(graphicsState);
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void ProcessSetRenderingIntent(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? intentName = operands[0].AsName();
        if (intentName == null)
        {
            return;
        }

        graphicsState.RenderingIntent = intentName.Value.AsEnum<PdfRenderingIntent>();
    }

    private void ProcessSaveGraphicsState(PdfGraphicsState graphicsState)
    {
        PdfGraphicsState savedState;

        if (_freeStates.Count > 0)
        {
            savedState = _freeStates.Pop();
            savedState.CopyFrom(graphicsState);
        }
        else
        {
            savedState = graphicsState.Clone();
        }

        _graphicsStack.Push(savedState);
        _processor.Process(SaveStateCommand.Instance);
    }

    private void ProcessRestoreGraphicsState(ref PdfGraphicsState graphicsState)
    {
        if (_graphicsStack.Count == 0)
        {
            return;
        }

        _freeStates.Push(graphicsState);
        graphicsState = _graphicsStack.Pop();
        _processor.Process(RestoreStateCommand.Instance);
    }

    private void ProcessConcatenateMatrix(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(6);
        if (operands.Length < 6)
        {
            return;
        }

        PdfMatrix matrix = PdfMatrix.FromOperands(operands) ?? PdfMatrix.Identity;
        _processor.Process(new ConcatMatrixCommand(matrix));

        // Update CTM in graphics state - concatenate with existing CTM
        graphicsState.CTM = matrix.PostConcat(graphicsState.CTM);
    }

    private void ProcessSetLineWidth(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        float? lineWidth = operands[0].AsFloat();
        if (lineWidth == null)
        {
            _logger.LogWarning("Skipping 'w' operator: non-numeric line width operand.");
            return;
        }

        PdfStrokeStyle strokeStyle = graphicsState.StrokePaint.RequireStrokeStyle().WithLineWidth(lineWidth.Value);
        graphicsState.StrokePaint = graphicsState.StrokePaint.WithStrokeStyle(strokeStyle);
    }

    private void ProcessSetLineCap(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        float? capStyle = operands[0].AsFloat();
        if (capStyle == null)
        {
            _logger.LogWarning("Skipping 'J' operator: non-numeric line cap operand.");
            return;
        }

        PdfStrokeCap lineCap = capStyle.Value switch
        {
            0 => PdfStrokeCap.Butt,
            1 => PdfStrokeCap.Round,
            2 => PdfStrokeCap.Square,
            _ => PdfStrokeCap.Butt
        };

        PdfStrokeStyle strokeStyle = graphicsState.StrokePaint.RequireStrokeStyle().WithLineCap(lineCap);
        graphicsState.StrokePaint = graphicsState.StrokePaint.WithStrokeStyle(strokeStyle);
    }

    private void ProcessSetLineJoin(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        float? joinStyle = operands[0].AsFloat();
        if (joinStyle == null)
        {
            _logger.LogWarning("Skipping 'j' operator: non-numeric line join operand.");
            return;
        }

        PdfStrokeJoin lineJoin = joinStyle.Value switch
        {
            0 => PdfStrokeJoin.Miter,
            1 => PdfStrokeJoin.Round,
            2 => PdfStrokeJoin.Bevel,
            _ => PdfStrokeJoin.Miter
        };

        PdfStrokeStyle strokeStyle = graphicsState.StrokePaint.RequireStrokeStyle().WithLineJoin(lineJoin);
        graphicsState.StrokePaint = graphicsState.StrokePaint.WithStrokeStyle(strokeStyle);
    }

    private void ProcessSetMiterLimit(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        float? miterLimit = operands[0].AsFloat();
        if (miterLimit == null)
        {
            _logger.LogWarning("Skipping 'M' operator: non-numeric miter limit operand.");
            return;
        }

        PdfStrokeStyle strokeStyle = graphicsState.StrokePaint.RequireStrokeStyle().WithMiterLimit(miterLimit.Value);
        graphicsState.StrokePaint = graphicsState.StrokePaint.WithStrokeStyle(strokeStyle);
    }

    private void ProcessSetDashPattern(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(2);
        if (operands.Length < 2)
        {
            return;
        }

        float? dashPhase = operands[1].AsFloat();
        if (dashPhase == null)
        {
            _logger.LogWarning("Skipping 'd' operator: non-numeric dash phase operand.");
            return;
        }

        float[]? dashArray = operands[0].AsArray()?.GetFloatArray();

        PdfStrokeStyle strokeStyle = (dashArray?.Length > 0)
            ? graphicsState.StrokePaint.RequireStrokeStyle().WithDash(GetDashPattern(dashArray), dashPhase.Value)
            : graphicsState.StrokePaint.RequireStrokeStyle().WithDash(null, 0f); // Empty array means solid line

        graphicsState.StrokePaint = graphicsState.StrokePaint.WithStrokeStyle(strokeStyle);
    }

    private void ProcessSetGraphicsStateParameters(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        PdfString? gsName = operands[0].AsName();
        if (gsName == null)
        {
            return;
        }

        _host.Resources.ApplyGraphicsStateParameters(gsName.Value, _processor, graphicsState);
    }

    private void ProcessSetFlatnessTolerance(PdfGraphicsState graphicsState)
    {
        ReadOnlySpan<PdfValue> operands = _operandStack.Pop(1);
        if (operands.Length == 0)
        {
            return;
        }

        float? flatness = operands[0].AsFloat();
        if (flatness == null)
        {
            _logger.LogWarning("Skipping 'i' operator: non-numeric flatness tolerance operand.");
            return;
        }

        graphicsState.FlatnessTolerance = flatness.Value;
    }

    public static float[]? GetDashPattern(float[] pattern)
    {
        if (pattern == null || pattern.Length == 0)
        {
            return null;
        }

        if (pattern.Length % 2 != 0)
        {
            // Per PDF spec, odd-length patterns are repeated to make even-length
            var extended = new float[pattern.Length * 2];
            Array.Copy(pattern, extended, pattern.Length);
            Array.Copy(pattern, 0, extended, pattern.Length, pattern.Length);
            pattern = extended;
        }

        const float epsilon = 0.001f;
        float sum = 0f;
        for (int i = 0; i < pattern.Length; i++)
        {
            float dash = pattern[i];

            if (dash > 0)
            {
                sum += dash;
            }
            else if (dash <= 0)
            {
                // Replace non-positive dashes with a small epsilon
                pattern[i] = epsilon;
            }
        }

        if (sum <= 0f)
        {
            // Per PDF spec, if the sum is zero, the line is solid (no dash effect)
            return null;
        }

        return pattern;
    }
}
