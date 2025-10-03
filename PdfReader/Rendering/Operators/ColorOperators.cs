using System.Collections.Generic;
using PdfReader.Models;
using SkiaSharp;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Pattern;
using PdfReader.Rendering; // For PdfPaint

namespace PdfReader.Rendering.Operators
{
    public static class ColorOperators
    {
        public static bool ProcessOperator(string op, Stack<IPdfValue> operandStack, PdfGraphicsState graphicsState, PdfPage page)
        {
            switch (op)
            {
                case "g":
                case "rg":
                case "k":
                    return ProcessDeviceFillColor(op, operandStack, graphicsState, page);
                case "G":
                case "RG":
                case "K":
                    return ProcessDeviceStrokeColor(op, operandStack, graphicsState, page);
                case "cs":
                    ProcessSetFillColorSpace(operandStack, graphicsState, page);
                    return true;
                case "CS":
                    ProcessSetStrokeColorSpace(operandStack, graphicsState, page);
                    return true;
                case "sc":
                    ProcessSetFillColor(operandStack, graphicsState, page);
                    return true;
                case "SC":
                    ProcessSetStrokeColor(operandStack, graphicsState, page);
                    return true;
                case "scn":
                    ProcessSetFillColorN(operandStack, graphicsState, page);
                    return true;
                case "SCN":
                    ProcessSetStrokeColorN(operandStack, graphicsState, page);
                    return true;
                default:
                    return false;
            }
        }

        private static bool ProcessDeviceFillColor(string op, Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            int expected;
            string space;
            switch (op)
            {
                case "g": expected = 1; space = PdfColorSpaceNames.DeviceGray; break;
                case "rg": expected = 3; space = PdfColorSpaceNames.DeviceRGB; break;
                case "k": expected = 4; space = PdfColorSpaceNames.DeviceCMYK; break;
                default: return false;
            }

            var operands = PdfOperatorProcessor.GetOperands(expected, operandStack);
            if (operands.Count < expected && expected > 1)
            {
                return false;
            }

            var converter = PdfColorSpaces.ResolveDeviceConverter(space, page);
            state.FillColorConverter = converter;

            var components = new float[converter.Components];
            for (int i = 0; i < components.Length && i < operands.Count; i++)
            {
                components[i] = operands[i].AsFloat();
            }

            var color = converter.ToSrgb(components, state.RenderingIntent);
            state.FillPaint = PdfPaint.Solid(color);
            return true;
        }

        private static bool ProcessDeviceStrokeColor(string op, Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            int expected;
            string space;
            switch (op)
            {
                case "G": expected = 1; space = PdfColorSpaceNames.DeviceGray; break;
                case "RG": expected = 3; space = PdfColorSpaceNames.DeviceRGB; break;
                case "K": expected = 4; space = PdfColorSpaceNames.DeviceCMYK; break;
                default: return false;
            }

            var operands = PdfOperatorProcessor.GetOperands(expected, operandStack);
            if (operands.Count < expected && expected > 1)
            {
                return false;
            }

            var converter = PdfColorSpaces.ResolveDeviceConverter(space, page);
            state.StrokeColorConverter = converter;

            var components = new float[converter.Components];
            for (int i = 0; i < components.Length && i < operands.Count; i++)
            {
                components[i] = operands[i].AsFloat();
            }

            var color = converter.ToSrgb(components, state.RenderingIntent);
            state.StrokePaint = PdfPaint.Solid(color);
            return true;
        }

        private static void ProcessSetFillColor(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.FillColorConverter;
            if (converter is PatternColorSpaceConverter)
            {
                return; // sc cannot select a pattern in Pattern CS
            }

            var components = new float[converter.Components];
            for (int i = 0; i < components.Length && i < operands.Count; i++)
            {
                components[i] = operands[i].AsFloat();
            }

            state.FillPaint = PdfPaint.Solid(converter.ToSrgb(components, state.RenderingIntent));
        }

        private static void ProcessSetStrokeColor(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
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
            for (int i = 0; i < components.Length && i < operands.Count; i++)
            {
                components[i] = operands[i].AsFloat();
            }

            state.StrokePaint = PdfPaint.Solid(converter.ToSrgb(components, state.RenderingIntent));
        }

        private static void ProcessSetFillColorN(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.FillColorConverter;
            if (converter is PatternColorSpaceConverter patternConverter)
            {
                var resolvedPattern = TryResolvePattern(operands[operands.Count - 1].AsName(), page);
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
                else if (resolvedPattern is PdfShadingPattern shadingPattern)
                {
                    // Shading pattern acts like colored pattern. No tint components.
                    state.FillPaint = PdfPaint.PatternFill(shadingPattern, null, SKColors.Black);
                    return;
                }
                state.FillPaint = PdfPaint.Solid(SKColors.Black);
                return;
            }

            var numericComponents = new float[converter.Components];
            for (int i = 0; i < numericComponents.Length && i < operands.Count; i++)
            {
                numericComponents[i] = operands[i].AsFloat();
            }
            state.FillPaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent));
        }

        private static void ProcessSetStrokeColorN(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
            if (operands.Count == 0)
            {
                return;
            }

            var converter = state.StrokeColorConverter;
            if (converter is PatternColorSpaceConverter patternConverter)
            {
                var resolvedPattern = TryResolvePattern(operands[operands.Count - 1].AsName(), page);
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
                else if (resolvedPattern is PdfShadingPattern shadingPattern)
                {
                    state.StrokePaint = PdfPaint.PatternFill(shadingPattern, null, SKColors.Black);
                    return;
                }
                state.StrokePaint = PdfPaint.Solid(SKColors.Black);
                return;
            }

            var numericComponents = new float[converter.Components];
            for (int i = 0; i < numericComponents.Length && i < operands.Count; i++)
            {
                numericComponents[i] = operands[i].AsFloat();
            }
            state.StrokePaint = PdfPaint.Solid(converter.ToSrgb(numericComponents, state.RenderingIntent));
        }

        private static void ProcessSetFillColorSpace(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
            if (operands.Count == 0)
            {
                return;
            }
            var raw = operands[0];
            state.FillColorConverter = PdfColorSpaces.ResolveByValue(raw, page);
            state.FillPaint = PdfPaint.Solid(SKColors.Black);
        }

        private static void ProcessSetStrokeColorSpace(Stack<IPdfValue> operandStack, PdfGraphicsState state, PdfPage page)
        {
            var operands = PopAllOperands(operandStack);
            if (operands.Count == 0)
            {
                return;
            }
            var raw = operands[0];
            state.StrokeColorConverter = PdfColorSpaces.ResolveByValue(raw, page);
            state.StrokePaint = PdfPaint.Solid(SKColors.Black);
        }

        private static List<IPdfValue> PopAllOperands(Stack<IPdfValue> operandStack)
        {
            var list = new List<IPdfValue>();
            while (operandStack.Count > 0)
            {
                list.Insert(0, operandStack.Pop());
            }
            return list;
        }

        private static PdfPattern TryResolvePattern(string patternName, PdfPage page)
        {
            try
            {
                var patternsDictionary = page.ResourceDictionary?.GetDictionary(PdfTokens.PatternKey);
                var patternObject = patternsDictionary?.GetPageObject(patternName);
                if (patternObject == null)
                {
                    return null;
                }
                return PdfPatternParser.TryParsePattern(patternObject.Reference, patternObject);
            }
            catch
            {
                return null;
            }
        }

        private static float[] ExtractTintComponents(PatternColorSpaceConverter patternConverter, List<IPdfValue> operands)
        {
            if (patternConverter == null || patternConverter.BaseColorSpace == null || patternConverter.BaseColorSpace.Components <= 0 || operands.Count <= 1)
            {
                return null;
            }
            int componentCount = patternConverter.BaseColorSpace.Components;
            var values = new float[componentCount];
            int provided = operands.Count - 1; // last operand is pattern name
            for (int i = 0; i < componentCount && i < provided; i++)
            {
                values[i] = operands[i].AsFloat();
            }
            return values;
        }
    }
}