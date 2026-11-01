using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Parsing;
using System;
using PdfPixel.Objects;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Operators;

/// <summary>
/// Handles color space and color setting operators (stroking and non-stroking).
/// Converted to instance implementation conforming to <see cref="IOperatorProcessor"/>.
/// </summary>
internal class ColorOperators : IOperatorProcessor
{
    private readonly PdfOperandStack _operandStack;
    private readonly IPdfContentHost _host;

    public ColorOperators(PdfOperandStack operandStack, IPdfContentHost host)
    {
        _operandStack = operandStack;
        _host = host;
    }

    public bool ProcessOperator(PdfOperator op, PdfParseContext context, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case PdfOperator.FillGray:
            case PdfOperator.FillRgb:
            case PdfOperator.FillCmyk:
            {
                ProcessDeviceFillColor(op, graphicsState);
                break;
            }
            case PdfOperator.StrokeGray:
            case PdfOperator.StrokeRgb:
            case PdfOperator.StrokeCmyk:
            {
                ProcessDeviceStrokeColor(op, graphicsState);
                break;
            }
            case PdfOperator.FillColorSpace:
            {
                ProcessSetFillColorSpace(context, graphicsState);
                break;
            }
            case PdfOperator.StrokeColorSpace:
            {
                ProcessSetStrokeColorSpace(context, graphicsState);
                break;
            }
            case PdfOperator.FillColor:
            {
                ProcessSetFillColor(graphicsState);
                break;
            }
            case PdfOperator.StrokeColor:
            {
                ProcessSetStrokeColor(graphicsState);
                break;
            }
            case PdfOperator.FillColorN:
            {
                ProcessSetFillColorN(context, graphicsState);
                break;
            }
            case PdfOperator.StrokeColorN:
            {
                ProcessSetStrokeColorN(context, graphicsState);
                break;
            }
            default:
            {
                return false;
            }
        }

        return true;
    }

    private void ProcessDeviceFillColor(PdfOperator op, PdfGraphicsState state)
    {
        int expected;
        PdfColorSpaceType space;
        switch (op)
        {
            case PdfOperator.FillGray:
            {
                expected = 1;
                space = PdfColorSpaceType.DeviceGray;
                break;
            }
            case PdfOperator.FillRgb:
            {
                expected = 3;
                space = PdfColorSpaceType.DeviceRGB;
                break;
            }
            case PdfOperator.FillCmyk:
            {
                expected = 4;
                space = PdfColorSpaceType.DeviceCMYK;
                break;
            }
            default:
            {
                return;
            }
        }

        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(expected);
        if (operands.Length < expected && expected > 1)
        {
            return;
        }

        PdfColorSpaceConverter converter = PdfColorSpaceResolver.ResolveDeviceConverter(space);

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.FillPaint = state.FillPaint.WithSolidColor(converter, _host.Resources.DeviceColorMapping, new PdfColor(components));
    }

    private void ProcessDeviceStrokeColor(PdfOperator op, PdfGraphicsState state)
    {
        int expected;
        PdfColorSpaceType space;
        switch (op)
        {
            case PdfOperator.StrokeGray:
            {
                expected = 1;
                space = PdfColorSpaceType.DeviceGray;
                break;
            }
            case PdfOperator.StrokeRgb:
            {
                expected = 3;
                space = PdfColorSpaceType.DeviceRGB;
                break;
            }
            case PdfOperator.StrokeCmyk:
            {
                expected = 4;
                space = PdfColorSpaceType.DeviceCMYK;
                break;
            }
            default:
            {
                return;
            }
        }

        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(expected);
        if (operands.Length < expected && expected > 1)
        {
            return;
        }

        PdfColorSpaceConverter converter = PdfColorSpaceResolver.ResolveDeviceConverter(space);

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.StrokePaint = state.StrokePaint.WithSolidColor(converter, _host.Resources.DeviceColorMapping, new PdfColor(components));
    }

    private void ProcessSetFillColor(PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfPaint fillPaint = state.FillPaint;
        var components = new float[fillPaint.ColorSpace.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.FillPaint = fillPaint.WithSolidColor(fillPaint.ColorSpace, fillPaint.DeviceColorMapping, new PdfColor(components));
    }

    private void ProcessSetStrokeColor(PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }


        PdfPaint strokePaint = state.StrokePaint;
        var components = new float[strokePaint.ColorSpace.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.StrokePaint = strokePaint.WithSolidColor(strokePaint.ColorSpace, strokePaint.DeviceColorMapping, new PdfColor(components));
    }

    private void ProcessSetFillColorN(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfPaint fillPaint = state.FillPaint;
        PdfColorSpaceConverter converter = fillPaint.ColorSpace;
        if (converter is PdfPatternColorSpaceConverter)
        {
            PdfString? patternName = operands[operands.Length - 1].GetPdfValue(context).AsName();
            PdfPattern? resolvedPattern = (patternName == null) ? null : _host.Resources.GetPattern(patternName.Value);

            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                if (tilingPattern.PaintType == PdfTilingPaintType.Uncolored)
                {
                    state.FillPaint = fillPaint.WithPattern(tilingPattern, ExtractTintComponents(operands));
                }
                else
                {
                    state.FillPaint = fillPaint.WithPattern(tilingPattern, PdfColor.Empty);
                }

                return;
            }
            else if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.FillPaint = fillPaint.WithPattern(shadingPattern, PdfColor.Empty);
                return;
            }

            state.FillPaint = fillPaint.WithSolidColor(converter, fillPaint.DeviceColorMapping, PdfColor.Empty);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.FillPaint = fillPaint.WithSolidColor(converter, fillPaint.DeviceColorMapping, new PdfColor(numericComponents));
    }

    private void ProcessSetStrokeColorN(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfPaint strokePaint = state.StrokePaint;
        PdfColorSpaceConverter converter = strokePaint.ColorSpace;
        if (converter is PdfPatternColorSpaceConverter)
        {
            PdfString? patternName = operands[operands.Length - 1].GetPdfValue(context).AsName();
            PdfPattern? resolvedPattern = (patternName == null) ? null : _host.Resources.GetPattern(patternName.Value);
            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                if (tilingPattern.PaintType == PdfTilingPaintType.Uncolored)
                {
                    state.StrokePaint = strokePaint.WithPattern(tilingPattern, ExtractTintComponents(operands));
                }
                else
                {
                    state.StrokePaint = strokePaint.WithPattern(tilingPattern, PdfColor.Empty);
                }

                return;
            }
            else if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.StrokePaint = strokePaint.WithPattern(shadingPattern, PdfColor.Empty);
                return;
            }

            state.StrokePaint = strokePaint.WithSolidColor(converter, strokePaint.DeviceColorMapping, PdfColor.Empty);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.StrokePaint = strokePaint.WithSolidColor(converter, strokePaint.DeviceColorMapping, new PdfColor(numericComponents));
    }

    private void ProcessSetFillColorSpace(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfValue? raw = operands[0].GetPdfValue(context);
        if (raw == null)
        {
            return;
        }

        PdfColorSpaceConverter converter = _host.Resources.ResolveColorSpace(raw) ?? PdfDeviceRgbColorSpaceConverter.Instance;

        state.FillPaint = state.FillPaint.WithSolidColor(converter, _host.Resources.DeviceColorMapping, new PdfColor(converter.GetInitialComponents()));
    }

    private void ProcessSetStrokeColorSpace(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfValue? raw = operands[0].GetPdfValue(context);
        if (raw == null)
        {
            return;
        }

        PdfColorSpaceConverter converter = _host.Resources.ResolveColorSpace(raw) ?? PdfDeviceRgbColorSpaceConverter.Instance;

        state.StrokePaint = state.StrokePaint.WithSolidColor(converter, _host.Resources.DeviceColorMapping, new PdfColor(converter.GetInitialComponents()));
    }

    private PdfColor ExtractTintComponents(in ReadOnlySpan<PdfContentStreamValue> operands)
    {
        if (operands.Length <= 1)
        {
            return PdfColor.Empty;
        }

        int provided = operands.Length - 1; // last operand is pattern name
        var values = new float[provided];

        for (int componentIndex = 0; componentIndex < provided; componentIndex++)
        {
            values[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        return new PdfColor(values);
    }
}
