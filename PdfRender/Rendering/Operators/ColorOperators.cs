using System.Collections.Generic;
using PdfRender.Models;
using SkiaSharp;
using PdfRender.Color.ColorSpace;
using PdfRender.Pattern.Model;
using PdfRender.Color.Paint;
using PdfRender.Rendering.State;
using PdfRender.Color.Transform;
using System;

namespace PdfRender.Rendering.Operators;

/// <summary>
/// Handles color space and color setting operators (stroking and non-stroking).
/// Converted to instance implementation conforming to <see cref="IOperatorProcessor"/>.
/// </summary>
public class ColorOperators : IOperatorProcessor
{
    private static readonly HashSet<string> SupportedOperators = new HashSet<string>
    {
        // Device color setting (non-stroking)
        "g","rg","k",
        // Device color setting (stroking)
        "G","RG","K",
        // Color space selection
        "cs","CS",
        // Generic color setting in current color space
        "sc","SC",
        // Extended color setting allowing patterns (Name operand allowed)
        "scn","SCN"
    };

    private readonly IPdfRenderer _renderer;
    private readonly Stack<IPdfValue> _operandStack;
    private readonly PdfPage _page;

    public ColorOperators(IPdfRenderer renderer, Stack<IPdfValue> operandStack, PdfPage page)
    {
        _renderer = renderer;
        _operandStack = operandStack;
        _page = page;
    }

    public bool CanProcess(string op)
    {
        return SupportedOperators.Contains(op);
    }

    public void ProcessOperator(string op, ref PdfGraphicsState graphicsState)
    {
        switch (op)
        {
            case "g":
            case "rg":
            case "k":
            {
                ProcessDeviceFillColor(op, graphicsState);
                break;
            }
            case "G":
            case "RG":
            case "K":
            {
                ProcessDeviceStrokeColor(op, graphicsState);
                break;
            }
            case "cs":
            {
                ProcessSetFillColorSpace(graphicsState);
                break;
            }
            case "CS":
            {
                ProcessSetStrokeColorSpace(graphicsState);
                break;
            }
            case "sc":
            {
                ProcessSetFillColor(graphicsState);
                break;
            }
            case "SC":
            {
                ProcessSetStrokeColor(graphicsState);
                break;
            }
            case "scn":
            {
                ProcessSetFillColorN(graphicsState);
                break;
            }
            case "SCN":
            {
                ProcessSetStrokeColorN(graphicsState);
                break;
            }
        }
    }

    private void ProcessDeviceFillColor(string op, PdfGraphicsState state)
    {
        int expected;
        PdfColorSpaceType space;
        switch (op)
        {
            case "g":
            {
                expected = 1;
                space = PdfColorSpaceType.DeviceGray;
                break;
            }
            case "rg":
            {
                expected = 3;
                space = PdfColorSpaceType.DeviceRGB;
                break;
            }
            case "k":
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

        var operands = PdfOperatorProcessor.GetOperands(expected, _operandStack);
        if (operands.Count < expected && expected > 1)
        {
            return;
        }

        var converter = _page.Cache.ColorSpace.ResolveDeviceConverter(space);
        state.FillColorConverter = converter;

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Count; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat();
        }

        var color = converter.ToSrgb(components, state.RenderingIntent, state.FullTransferFunction);
        state.FillPaint = PdfPaint.Solid(color);
    }

    private void ProcessDeviceStrokeColor(string op, PdfGraphicsState state)
    {
        int expected;
        PdfColorSpaceType space;
        switch (op)
        {
            case "G":
            {
                expected = 1;
                space = PdfColorSpaceType.DeviceGray;
                break;
            }
            case "RG":
            {
                expected = 3;
                space = PdfColorSpaceType.DeviceRGB;
                break;
            }
            case "K":
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

        var operands = PdfOperatorProcessor.GetOperands(expected, _operandStack);
        if (operands.Count < expected && expected > 1)
        {
            return;
        }

        var converter = _page.Cache.ColorSpace.ResolveDeviceConverter(space);
        state.StrokeColorConverter = converter;

        var components = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Count; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat();
        }

        var color = converter.ToSrgb(components, state.RenderingIntent, state.FullTransferFunction);
        state.StrokePaint = PdfPaint.Solid(color);
    }

    private void ProcessSetFillColor(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }

        Span<float> components = stackalloc float[state.FillColorConverter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat();
        }

        state.FillPaint = PdfPaint.Solid(state.FillRgbaSampler.Sample(components).From01ToSkiaColor());
    }

    private void ProcessSetStrokeColor(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }


        Span<float> components = stackalloc float[state.StrokeColorConverter.Components];
        for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Length; componentIndex++)
        {
            components[componentIndex] = operands[componentIndex].AsFloat();
        }

        state.StrokePaint = PdfPaint.Solid(state.StrokeRgbaSampler.Sample(components).From01ToSkiaColor());
    }

    private void ProcessSetFillColorN(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }

        var converter = state.FillColorConverter;
        if (converter is PatternColorSpaceConverter patternConverter)
        {
            var patternName = operands[operands.Length - 1].AsName();
            var resolvedPattern = _page.Cache.GetPattern(_renderer, patternName);
            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                var tintColor = SKColors.Black;

                if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored)
                {
                    var tintComponents = ExtractTintComponents(operands);
                    tintColor = state.FillRgbaSampler.Sample(tintComponents).From01ToSkiaColor();
                }

                state.FillPaint = PdfPaint.PatternFill(tilingPattern, tintColor);
                return;
            }
            if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.FillPaint = PdfPaint.PatternFill(shadingPattern, SKColors.Black);
                return;
            }
            state.FillPaint = PdfPaint.Solid(SKColors.Black);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat();
        }
        state.FillPaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent, state.FullTransferFunction));
    }

    private void ProcessSetStrokeColorN(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }

        var converter = state.StrokeColorConverter;
        if (converter is PatternColorSpaceConverter patternConverter)
        {
            var patternName = operands[operands.Length - 1].AsName();
            var resolvedPattern = _page.Cache.GetPattern(_renderer, patternName);
            if (resolvedPattern is PdfTilingPattern tilingPattern)
            {
                var tintColor = SKColors.Black;

                if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored)
                {
                    var tintComponents = ExtractTintComponents(operands);
                    tintColor = state.StrokeRgbaSampler.Sample(tintComponents).From01ToSkiaColor();
                }

                state.StrokePaint = PdfPaint.PatternFill(tilingPattern, tintColor);
                return;
            }
            if (resolvedPattern is PdfShadingPattern shadingPattern)
            {
                state.StrokePaint = PdfPaint.PatternFill(shadingPattern, SKColors.Black);
                return;
            }
            state.StrokePaint = PdfPaint.Solid(SKColors.Black);
            return;
        }

        var numericComponents = new float[converter.Components];
        for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Length; componentIndex++)
        {
            numericComponents[componentIndex] = operands[componentIndex].AsFloat();
        }
        state.StrokePaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent, state.FullTransferFunction));
    }

    private void ProcessSetFillColorSpace(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }
        var raw = operands[0];
        state.FillColorConverter = _page.Cache.ColorSpace.ResolveByValue(raw);
        state.FillPaint = PdfPaint.Solid(SKColors.Black);
    }

    private void ProcessSetStrokeColorSpace(PdfGraphicsState state)
    {
        var operands = PopAllOperands();
        if (operands.Length == 0)
        {
            return;
        }
        var raw = operands[0];
        state.StrokeColorConverter = _page.Cache.ColorSpace.ResolveByValue(raw);
        state.StrokePaint = PdfPaint.Solid(SKColors.Black);
    }

    private IPdfValue[] PopAllOperands()
    {
        int count = _operandStack.Count;
        if (count == 0)
        {
            return Array.Empty<IPdfValue>();
        }

        var array = new IPdfValue[count];
        for (int i = count - 1; i >= 0; i--)
        {
            array[i] = _operandStack.Pop();
        }
        return array;
    }

    private ReadOnlySpan<float> ExtractTintComponents(IPdfValue[] operands)
    {
        if (operands.Length <= 1)
        {
            return ReadOnlySpan<float>.Empty;
        }

        int provided = operands.Length - 1; // last operand is pattern name
        Span<float> values = new float[provided];

        for (int componentIndex = 0; componentIndex < provided; componentIndex++)
        {
            values[componentIndex] = operands[componentIndex].AsFloat();
        }

        return values;
    }
}