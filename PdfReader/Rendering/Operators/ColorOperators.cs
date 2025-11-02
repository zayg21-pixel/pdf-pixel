using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Pattern;

namespace PdfReader.Rendering.Operators
{
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

        private readonly Stack<IPdfValue> _operandStack;
        private readonly PdfPage _page;

        public ColorOperators(Stack<IPdfValue> operandStack, PdfPage page)
        {
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
            PdfColorSpace space;
            switch (op)
            {
                case "g":
                {
                    expected = 1;
                    space = PdfColorSpace.DeviceGray;
                    break;
                }
                case "rg":
                {
                    expected = 3;
                    space = PdfColorSpace.DeviceRGB;
                    break;
                }
                case "k":
                {
                    expected = 4;
                    space = PdfColorSpace.DeviceCMYK;
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

            var color = converter.ToSrgb(components, state.RenderingIntent);
            state.FillPaint = PdfPaint.Solid(color);
        }

        private void ProcessDeviceStrokeColor(string op, PdfGraphicsState state)
        {
            int expected;
            PdfColorSpace space;
            switch (op)
            {
                case "G":
                {
                    expected = 1;
                    space = PdfColorSpace.DeviceGray;
                    break;
                }
                case "RG":
                {
                    expected = 3;
                    space = PdfColorSpace.DeviceRGB;
                    break;
                }
                case "K":
                {
                    expected = 4;
                    space = PdfColorSpace.DeviceCMYK;
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
                components[componentIndex] = operands[componentIndex].ResolveToNonReference(_page.Document).AsFloat();
            }

            var color = converter.ToSrgb(components, state.RenderingIntent);
            state.StrokePaint = PdfPaint.Solid(color);
        }

        private void ProcessSetFillColor(PdfGraphicsState state)
        {
            var operands = PopAllOperands();
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.FillColorConverter;
            if (converter is PatternColorSpaceConverter)
            {
                // sc cannot select a pattern when current color space is Pattern per spec.
                return;
            }

            var components = new float[converter.Components];
            for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Count; componentIndex++)
            {
                components[componentIndex] = operands[componentIndex].AsFloat();
            }

            state.FillPaint = PdfPaint.Solid(converter.ToSrgb(components, state.RenderingIntent));
        }

        private void ProcessSetStrokeColor(PdfGraphicsState state)
        {
            var operands = PopAllOperands();
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.StrokeColorConverter;
            if (converter is PatternColorSpaceConverter)
            {
                return;
            }

            var components = new float[converter.Components];
            for (int componentIndex = 0; componentIndex < components.Length && componentIndex < operands.Count; componentIndex++)
            {
                components[componentIndex] = operands[componentIndex].AsFloat();
            }

            state.StrokePaint = PdfPaint.Solid(converter.ToSrgb(components, state.RenderingIntent));
        }

        private void ProcessSetFillColorN(PdfGraphicsState state)
        {
            var operands = PopAllOperands();
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.FillColorConverter;
            if (converter is PatternColorSpaceConverter patternConverter)
            {
                var patternName = operands[operands.Count - 1].AsName();
                var resolvedPattern = TryResolvePattern(patternName);
                if (resolvedPattern is PdfTilingPattern tilingPattern)
                {
                    var tintComponents = ExtractTintComponents(patternConverter, operands);
                    var tintColor = SKColors.Black;
                    if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored && tintComponents != null && patternConverter.BaseColorSpace != null)
                    {
                        tintColor = patternConverter.BaseColorSpace.ToSrgb(tintComponents, state.RenderingIntent);
                    }
                    state.FillPaint = PdfPaint.PatternFill(tilingPattern, tintComponents, tintColor);
                    return;
                }
                if (resolvedPattern is PdfShadingPattern shadingPattern)
                {
                    state.FillPaint = PdfPaint.PatternFill(shadingPattern, null, SKColors.Black);
                    return;
                }
                state.FillPaint = PdfPaint.Solid(SKColors.Black);
                return;
            }

            var numericComponents = new float[converter.Components];
            for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Count; componentIndex++)
            {
                numericComponents[componentIndex] = operands[componentIndex].AsFloat();
            }
            state.FillPaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent));
        }

        private void ProcessSetStrokeColorN(PdfGraphicsState state)
        {
            var operands = PopAllOperands();
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.StrokeColorConverter;
            if (converter is PatternColorSpaceConverter patternConverter)
            {
                var patternName = operands[operands.Count - 1].AsName();
                var resolvedPattern = TryResolvePattern(patternName);
                if (resolvedPattern is PdfTilingPattern tilingPattern)
                {
                    var tintComponents = ExtractTintComponents(patternConverter, operands);
                    var tintColor = SKColors.Black;
                    if (tilingPattern.PaintTypeKind == PdfTilingPaintType.Uncolored && tintComponents != null && patternConverter.BaseColorSpace != null)
                    {
                        tintColor = patternConverter.BaseColorSpace.ToSrgb(tintComponents, state.RenderingIntent);
                    }
                    state.StrokePaint = PdfPaint.PatternFill(tilingPattern, tintComponents, tintColor);
                    return;
                }
                if (resolvedPattern is PdfShadingPattern shadingPattern)
                {
                    state.StrokePaint = PdfPaint.PatternFill(shadingPattern, null, SKColors.Black);
                    return;
                }
                state.StrokePaint = PdfPaint.Solid(SKColors.Black);
                return;
            }

            var numericComponents = new float[converter.Components];
            for (int componentIndex = 0; componentIndex < numericComponents.Length && componentIndex < operands.Count; componentIndex++)
            {
                numericComponents[componentIndex] = operands[componentIndex].AsFloat();
            }
            state.StrokePaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent));
        }

        private void ProcessSetFillColorSpace(PdfGraphicsState state)
        {
            var operands = PopAllOperands();
            if (operands.Count == 0)
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
            if (operands.Count == 0)
            {
                return;
            }
            var raw = operands[0];
            state.StrokeColorConverter = _page.Cache.ColorSpace.ResolveByValue(raw);
            state.StrokePaint = PdfPaint.Solid(SKColors.Black);
        }

        private List<IPdfValue> PopAllOperands()
        {
            var list = new List<IPdfValue>();
            while (_operandStack.Count > 0)
            {
                list.Insert(0, _operandStack.Pop());
            }
            return list;
        }

        private PdfPattern TryResolvePattern(PdfString patternName)
        {
            try
            {
                var patternsDictionary = _page.ResourceDictionary?.GetDictionary(PdfTokens.PatternKey);
                var patternObject = patternsDictionary?.GetPageObject(patternName);
                if (patternObject == null)
                {
                    return null;
                }
                return PdfPatternParser.ParsePattern(patternObject, _page);
            }
            catch
            {
                return null;
            }
        }

        private float[] ExtractTintComponents(PatternColorSpaceConverter patternConverter, List<IPdfValue> operands)
        {
            if (patternConverter == null)
            {
                return null;
            }
            if (patternConverter.BaseColorSpace == null)
            {
                return null;
            }
            if (patternConverter.BaseColorSpace.Components <= 0)
            {
                return null;
            }
            if (operands.Count <= 1)
            {
                return null;
            }

            int componentCount = patternConverter.BaseColorSpace.Components;
            var values = new float[componentCount];
            int provided = operands.Count - 1; // last operand is pattern name
            for (int componentIndex = 0; componentIndex < componentCount && componentIndex < provided; componentIndex++)
            {
                values[componentIndex] = operands[componentIndex].AsFloat();
            }
            return values;
        }
    }
}