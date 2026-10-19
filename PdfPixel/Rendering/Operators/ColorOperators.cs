using PdfPixel.Models;
using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Parsing;
using PdfPixel.Pattern.Model;
using PdfPixel.Rendering.State;
using PdfPixel.Color.Transform;
using System;

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
        state.FillColorConverter = converter;
        state.FillDeviceColorMapping = _host.Resources.DeviceColorMapping;

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        PdfColor color = converter.ToSrgb(components, new PdfColorConversionParameters(state, state.FillDeviceColorMapping));
        state.FillPaint = state.FillPaint.WithSolidColor(color);
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
        state.StrokeColorConverter = converter;
        state.StrokeDeviceColorMapping = _host.Resources.DeviceColorMapping;

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        PdfColor color = converter.ToSrgb(components, new PdfColorConversionParameters(state, state.StrokeDeviceColorMapping));
        state.StrokePaint = state.StrokePaint.WithSolidColor(color);
    }

    private void ProcessSetFillColor(PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        Span<float> components = stackalloc float[state.FillColorConverter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.FillPaint = state.FillPaint.WithSolidColor(state.FillRgbaSampler.Sample(components).ToPdfColor());
    }

    private void ProcessSetStrokeColor(PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }


        Span<float> components = stackalloc float[state.StrokeColorConverter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.StrokePaint = state.StrokePaint.WithSolidColor(state.StrokeRgbaSampler.Sample(components).ToPdfColor());
    }

    private void ProcessSetFillColorN(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfColorSpaceConverter converter = state.FillColorConverter;
        if (converter is PdfPatternColorSpaceConverter)
        {
            PdfString? patternName = operands[operands.Length - 1].GetPdfValue(context).AsName();
            PdfPattern? resolvedPattern = (patternName == null) ? null : _host.Resources.GetPattern(patternName.Value);

            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                PdfColor tintColor = PdfColors.Black;

                if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored)
                {
                    ReadOnlySpan<float> tintComponents = ExtractTintComponents(operands);
                    tintColor = state.FillRgbaSampler.Sample(tintComponents).ToPdfColor();
                }

                state.FillPaint = state.FillPaint.WithPattern(tilingPattern, tintColor);
                return;
            }
            else if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.FillPaint = state.FillPaint.WithPattern(shadingPattern, PdfColors.Black);
                return;
            }

            state.FillPaint = state.FillPaint.WithSolidColor(PdfColors.Black);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.FillPaint = state.FillPaint.WithSolidColor(converter.ToSrgb(numericComponents, new PdfColorConversionParameters(state, state.FillDeviceColorMapping)));
    }

    private void ProcessSetStrokeColorN(PdfParseContext context, PdfGraphicsState state)
    {
        ReadOnlySpan<PdfContentStreamValue> operands = _operandStack.Pop(_operandStack.Count);
        if (operands.Length == 0)
        {
            return;
        }

        PdfColorSpaceConverter converter = state.StrokeColorConverter;
        if (converter is PdfPatternColorSpaceConverter)
        {
            PdfString? patternName = operands[operands.Length - 1].GetPdfValue(context).AsName();
            PdfPattern? resolvedPattern = (patternName == null) ? null : _host.Resources.GetPattern(patternName.Value);
            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                PdfColor tintColor = PdfColors.Black;

                if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored)
                {
                    ReadOnlySpan<float> tintComponents = ExtractTintComponents(operands);
                    tintColor = state.StrokeRgbaSampler.Sample(tintComponents).ToPdfColor();
                }

                state.StrokePaint = state.StrokePaint.WithPattern(tilingPattern, tintColor);
                return;
            }
            else if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.StrokePaint = state.StrokePaint.WithPattern(shadingPattern, PdfColors.Black);
                return;
            }

            state.StrokePaint = state.StrokePaint.WithSolidColor(PdfColors.Black);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        state.StrokePaint = state.StrokePaint.WithSolidColor(converter.ToSrgb(numericComponents, new PdfColorConversionParameters(state, state.StrokeDeviceColorMapping)));
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
        state.FillColorConverter = converter;
        state.FillDeviceColorMapping = _host.Resources.DeviceColorMapping;

        PdfColor initialColor = (converter is PdfPatternColorSpaceConverter)
            ? PdfColors.Black
            : state.FillRgbaSampler.Sample(converter.GetInitialComponents()).ToPdfColor();
        state.FillPaint = state.FillPaint.WithSolidColor(initialColor);
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
        state.StrokeColorConverter = converter;
        state.StrokeDeviceColorMapping = _host.Resources.DeviceColorMapping;

        PdfColor initialColor = (converter is PdfPatternColorSpaceConverter)
            ? PdfColors.Black
            : state.StrokeRgbaSampler.Sample(converter.GetInitialComponents()).ToPdfColor();
        state.StrokePaint = state.StrokePaint.WithSolidColor(initialColor);
    }

    private ReadOnlySpan<float> ExtractTintComponents(in ReadOnlySpan<PdfContentStreamValue> operands)
    {
        if (operands.Length <= 1)
        {
            return ReadOnlySpan<float>.Empty;
        }

        int provided = operands.Length - 1; // last operand is pattern name
        Span<float> values = new float[provided];

        for (int componentIndex = 0; componentIndex < provided; componentIndex++)
        {
            values[componentIndex] = operands[componentIndex].AsFloat() ?? 0f;
        }

        return values;
    }
}
