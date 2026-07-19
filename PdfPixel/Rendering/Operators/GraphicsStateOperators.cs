using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Operators;

internal class GraphicsStateOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = [
        "q","Q","cm","w","J","j","M","d","gs","ri","i"
    ];

    private readonly IPdfPageInternal _page;
    private readonly IPdfCommandProcessor _processor;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly Stack<PdfGraphicsState> _graphicsStack;
    private readonly ILogger<GraphicsStateOperators> _logger;

    public GraphicsStateOperators(IPdfPageInternal page, IPdfCommandProcessor processor, Stack<IPdfValue> operandStack, Stack<PdfGraphicsState> graphicsStack)
    {
        _page = page;
        _processor = processor;
        _operandStack = operandStack;
        _graphicsStack = graphicsStack;
        _logger = page.Document.LoggerFactory.CreateLogger<GraphicsStateOperators>();
    }

    public bool CanProcess(string op) => SupportedOperators.Contains(op);

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "q":
            {
                // Save graphics state
                ProcessSaveGraphicsState(graphicsState);
                break;
            }
            case "Q":
            {
                // Restore graphics state
                ProcessRestoreGraphicsState(ref graphicsState);
                break;
            }
            case "cm":
            {
                // Concatenate matrix
                ProcessConcatenateMatrix(graphicsState);
                break;
            }
            case "w":
            {
                // Set line width
                ProcessSetLineWidth(graphicsState);
                break;
            }
            case "J":
            {
                // Set line cap style
                ProcessSetLineCap(graphicsState);
                break;
            }
            case "j":
            {
                // Set line join style
                ProcessSetLineJoin(graphicsState);
                break;
            }
            case "M":
            {
                // Set miter limit
                ProcessSetMiterLimit(graphicsState);
                break;
            }
            case "d":
            {
                // Set dash pattern
                ProcessSetDashPattern(graphicsState);
                break;
            }
            case "gs":
            {
                // Set graphics state parameters from ExtGState
                ProcessSetGraphicsStateParameters(graphicsState);
                break;
            }
            case "ri":
            {
                // Set rendering intent
                ProcessSetRenderingIntent(graphicsState);
                break;
            }
            case "i":
            {
                // Set flatness tolerance
                ProcessSetFlatnessTolerance(graphicsState);
                break;
            }
        }
    }

    private void ProcessSetRenderingIntent(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.RenderingIntent = operands[0].AsName().AsEnum<PdfRenderingIntent>();
    }

    private void ProcessSaveGraphicsState(PdfGraphicsState graphicsState)
    {
        _graphicsStack.Push(graphicsState.Clone());
        _processor.Process(SaveStateCommand.Instance);
    }

    private void ProcessRestoreGraphicsState(ref PdfGraphicsState graphicsState)
    {
        if (_graphicsStack.Count == 0)
        {
            return;
        }

        graphicsState = _graphicsStack.Pop();
        _processor.Process(RestoreStateCommand.Instance);
    }

    private void ProcessConcatenateMatrix(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(6, _operandStack);
        if (operands.Count < 6)
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
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.StrokePaint.RequireStrokeStyle().LineWidth = operands[0].AsFloat();
    }

    private void ProcessSetLineCap(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float capStyle = operands[0].AsFloat();
        graphicsState.StrokePaint.RequireStrokeStyle().LineCap = capStyle switch
        {
            0 => PdfStrokeCap.Butt,
            1 => PdfStrokeCap.Round,
            2 => PdfStrokeCap.Square,
            _ => PdfStrokeCap.Butt
        };
    }

    private void ProcessSetLineJoin(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float joinStyle = operands[0].AsFloat();
        graphicsState.StrokePaint.RequireStrokeStyle().LineJoin = joinStyle switch
        {
            0 => PdfStrokeJoin.Miter,
            1 => PdfStrokeJoin.Round,
            2 => PdfStrokeJoin.Bevel,
            _ => PdfStrokeJoin.Miter
        };
    }

    private void ProcessSetMiterLimit(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        graphicsState.StrokePaint.RequireStrokeStyle().MiterLimit = operands[0].AsFloat();
    }

    private void ProcessSetDashPattern(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(2, _operandStack);
        if (operands.Count < 2)
        {
            return;
        }

        float[]? dashArray = operands[0].AsArray()?.GetFloatArray();
        float dashPhase = operands[1].AsFloat();

        PdfStrokeStyle strokeStyle = graphicsState.StrokePaint.RequireStrokeStyle();
        if (dashArray?.Length > 0)
        {
            strokeStyle.DashPattern = GetDashPattern(dashArray);
            strokeStyle.DashPhase = dashPhase;
        }
        else
        {
            // Empty array means solid line
            strokeStyle.DashPattern = null;
            strokeStyle.DashPhase = 0;
        }
    }

    private void ProcessSetGraphicsStateParameters(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        PdfString gsName = operands[0].AsName();
        _page.Cache.ApplyGraphicsStateParameters(gsName, _processor, graphicsState);
    }

    private void ProcessSetFlatnessTolerance(PdfGraphicsState graphicsState)
    {
        List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(1, _operandStack);
        if (operands.Count == 0)
        {
            return;
        }

        float flatness = operands[0].AsFloat();
        graphicsState.FlatnessTolerance = flatness;
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
