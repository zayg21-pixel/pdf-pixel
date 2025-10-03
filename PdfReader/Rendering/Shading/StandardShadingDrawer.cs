using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering.Shading
{
    /// <summary>
    /// Draws axial (type 2) and radial (type 3) shadings (function based and simple interpolation).
    /// Implements 1D sampled (type 0) function decoding for gradient color evaluation.
    /// </summary>
    public class StandardShadingDrawer : IShadingDrawer
    {
        public void DrawShading(SKCanvas canvas, PdfDictionary shading, PdfGraphicsState state, PdfPage page)
        {
            if (shading == null)
            {
                return;
            }

            int type = shading.GetInteger(PdfTokens.ShadingTypeKey);
            switch (type)
            {
                case 2:
                    DrawAxialShading(shading, state, canvas, page);
                    break;
                case 3:
                    DrawRadialShading(shading, state, canvas, page);
                    break;
                default:
                    Console.WriteLine("Shading type " + type + " not implemented");
                    break;
            }
        }

        private static void DrawAxialShading(PdfDictionary shading, PdfGraphicsState gs, SKCanvas canvas, PdfPage page)
        {
            var coords = shading.GetArray(PdfTokens.CoordsKey);
            if (coords == null || coords.Count < 4)
            {
                return;
            }

            float x0 = coords[0].AsFloat();
            float y0 = coords[1].AsFloat();
            float x1 = coords[2].AsFloat();
            float y1 = coords[3].AsFloat();

            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, gs.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            // Determine tile mode based on extend flags
            SKShaderTileMode tileMode = SKShaderTileMode.Clamp;
            if (extendStart && extendEnd)
            {
                // Both ends extend - use clamp (SkiaSharp default behavior)
                tileMode = SKShaderTileMode.Clamp;
            }
            else if (!extendStart && !extendEnd)
            {
                // No extension - gradient should be clamped to defined region
                tileMode = SKShaderTileMode.Decal; // Or handle clipping manually
            }

            using (var shader = SKShader.CreateLinearGradient(new SKPoint(x0, y0), new SKPoint(x1, y1), colors, positions, tileMode))
            using (var paint = PdfPaintFactory.CreateShadingPaint(gs, shader, page))
            {
                canvas.DrawPaint(paint);
            }
        }

        private static void DrawRadialShading(PdfDictionary shading, PdfGraphicsState gs, SKCanvas canvas, PdfPage page)
        {
            var coords = shading.GetArray(PdfTokens.CoordsKey);
            if (coords == null || coords.Count < 6)
            {
                return;
            }

            float x0 = coords[0].AsFloat();
            float y0 = coords[1].AsFloat();
            float r0 = coords[2].AsFloat();
            float x1 = coords[3].AsFloat();
            float y1 = coords[4].AsFloat();
            float r1 = coords[5].AsFloat();
            if (r0 < 0) r0 = 0;
            if (r1 < 0) r1 = 0;

            GetExtend(shading, out bool extendStart, out bool extendEnd);

            BuildShadingColorsAndStops(shading, page, gs.RenderingIntent, out var colors, out var positions);
            if (colors == null || colors.Length == 0)
            {
                return;
            }

            if (r0 > r1)
            {
                (x0, x1) = (x1, x0);
                (y0, y1) = (y1, y0);
                (r0, r1) = (r1, r0);
                Array.Reverse(colors);
                Array.Reverse(positions);
                for (int i = 0; i < positions.Length; i++)
                {
                    positions[i] = 1f - positions[i];
                }
            }

            using (var shader = SKShader.CreateTwoPointConicalGradient(new SKPoint(x0, y0), r0, new SKPoint(x1, y1), r1, colors, positions, SKShaderTileMode.Clamp))
            using (var paint = PdfPaintFactory.CreateShadingPaint(gs, shader, page))
            {
                if (!extendStart || !extendEnd)
                {
                    SKPath clip = null;
                    if (!extendEnd)
                    {
                        clip = new SKPath();
                        clip.AddCircle(x1, y1, r1);
                    }
                    canvas.Save();
                    if (clip != null)
                    {
                        canvas.ClipPath(clip, antialias: true);
                    }
                    canvas.DrawPaint(paint);
                    canvas.Restore();
                }
                else
                {
                    canvas.DrawPaint(paint);
                }
            }
        }

        private static void GetExtend(PdfDictionary shading, out bool extendStart, out bool extendEnd)
        {
            extendStart = false;
            extendEnd = false;
            var extendArr = shading.GetArray(PdfTokens.ExtendKey);
            if (extendArr != null && extendArr.Count >= 2)
            {
                extendStart = extendArr[0].AsBool();
                extendEnd = extendArr[1].AsBool();
            }
        }

        private static void BuildShadingColorsAndStops(PdfDictionary shading, PdfPage page, PdfRenderingIntent intent, out SKColor[] colors, out float[] positions)
        {
            var csVal = shading.GetValue(PdfTokens.ColorSpaceKey);
            var converter = PdfColorSpaces.ResolveByValue(csVal, page) ?? DeviceRgbConverter.Instance;

            var domain = shading.GetArray(PdfTokens.DomainKey);
            float d0 = 0f;
            float d1 = 1f;
            if (domain != null && domain.Count >= 2)
            {
                d0 = domain[0].AsFloat();
                d1 = domain[1].AsFloat();
                if (Math.Abs(d1 - d0) < 1e-9f)
                {
                    d1 = d0 + 1f;
                }
            }

            var funcVal = shading.GetValue(PdfTokens.FunctionKey);
            if (funcVal != null)
            {
                const int SampleCount = 64; // basic sampling density
                colors = new SKColor[SampleCount];
                positions = new float[SampleCount];
                for (int i = 0; i < SampleCount; i++)
                {
                    float t = i / (float)(SampleCount - 1);
                    float x = d0 + t * (d1 - d0);
                    var comps = EvaluateFunction(shading, x);
                    colors[i] = converter.ToSrgb(comps, intent);
                    positions[i] = t;
                }
                return;
            }

            var c0Arr = shading.GetArray(PdfTokens.C0Key);
            var c1Arr = shading.GetArray(PdfTokens.C1Key);
            if (c0Arr != null && c1Arr != null)
            {
                var c0 = ArrayFromArray(c0Arr);
                var c1 = ArrayFromArray(c1Arr);
                colors = new[]
                {
                    converter.ToSrgb(c0, intent),
                    converter.ToSrgb(c1, intent)
                };
                positions = new[] { 0f, 1f };
                return;
            }

            colors = new[] { SKColors.Black, SKColors.White };
            positions = new[] { 0f, 1f };
        }

        private static float[] EvaluateFunction(PdfDictionary shading, float x)
        {
            var single = shading.GetPageObject(PdfTokens.FunctionKey);
            if (single != null && single.Dictionary != null && shading.GetPageObjects(PdfTokens.FunctionKey)?.Count <= 1)
            {
                return EvaluateFunctionObject(single, x);
            }

            var functions = shading.GetPageObjects(PdfTokens.FunctionKey);
            if (functions != null && functions.Count > 0)
            {
                var all = new List<float>();
                foreach (var f in functions)
                {
                    if (f?.Dictionary == null)
                    {
                        continue;
                    }
                    var vals = EvaluateFunctionObject(f, x);
                    if (vals != null && vals.Length > 0)
                    {
                        all.AddRange(vals);
                    }
                }
                return all.ToArray();
            }

            return Array.Empty<float>();
        }

        private static float[] EvaluateFunctionObject(PdfObject funcObj, float x)
        {
            if (funcObj == null || funcObj.Dictionary == null)
            {
                return Array.Empty<float>();
            }
            var dict = funcObj.Dictionary;
            int ftype = dict.GetInteger(PdfTokens.FunctionTypeKey);
            switch (ftype)
            {
                case 0:
                    return EvaluateSampledFunction1D(funcObj, x);
                case 2:
                    return EvaluateExponentialFunction(dict, x);
                case 3:
                    return EvaluateStitchingFunction(funcObj, x);
                default:
                    return Array.Empty<float>();
            }
        }

        private static float[] EvaluateExponentialFunction(PdfDictionary dict, float x)
        {
            var c0Arr = dict.GetArray(PdfTokens.C0Key);
            var c1Arr = dict.GetArray(PdfTokens.C1Key);
            float n = dict.GetFloat(PdfTokens.FnNKey);
            var c0 = c0Arr != null ? ArrayFromArray(c0Arr) : new float[] { 0 };
            var c1 = c1Arr != null ? ArrayFromArray(c1Arr) : new float[] { 1 };
            int k = Math.Min(c0.Length, c1.Length);
            var outp = new float[k];
            float clamped = x;
            if (clamped < 0f) clamped = 0f; else if (clamped > 1f) clamped = 1f;
            float xn = n <= 0 ? clamped : (float)Math.Pow(clamped, n);
            for (int i = 0; i < k; i++)
            {
                outp[i] = c0[i] + xn * (c1[i] - c0[i]);
            }
            return outp;
        }

        private static float[] EvaluateStitchingFunction(PdfObject funcObj, float x)
        {
            var dict = funcObj.Dictionary;
            var subFuncs = dict.GetPageObjects(PdfTokens.FunctionsKey);
            if (subFuncs == null || subFuncs.Count == 0)
            {
                return Array.Empty<float>();
            }
            var bounds = dict.GetArray(PdfTokens.BoundsKey);
            var encode = dict.GetArray(PdfTokens.EncodeKey);
            var domainArr = dict.GetArray(PdfTokens.DomainKey);
            float d0 = 0f, d1 = 1f;
            if (domainArr != null && domainArr.Count >= 2)
            {
                d0 = domainArr[0].AsFloat();
                d1 = domainArr[1].AsFloat();
            }
            int segment = 0;
            float[] b = null;
            if (bounds != null && bounds.Count > 0)
            {
                b = new float[bounds.Count];
                for (int i = 0; i < bounds.Count; i++) b[i] = bounds[i].AsFloat();
                while (segment < b.Length && x > b[segment]) segment++;
                if (segment >= subFuncs.Count) segment = subFuncs.Count - 1;
            }
            float xs = x;
            if (b != null && encode != null && encode.Count >= 2 * subFuncs.Count)
            {
                float a = segment == 0 ? d0 : b[segment - 1];
                float c = segment < b.Length ? b[segment] : d1;
                float e0 = encode[2 * segment].AsFloat();
                float e1 = encode[2 * segment + 1].AsFloat();
                float t = (c - a) != 0 ? (x - a) / (c - a) : 0f;
                xs = e0 + t * (e1 - e0);
            }
            return EvaluateFunctionObject(subFuncs[segment], xs);
        }

        private static float[] EvaluateSampledFunction1D(PdfObject funcObj, float x)
        {
            try
            {
                var dict = funcObj.Dictionary;
                var domainArr = dict.GetArray(PdfTokens.DomainKey);
                if (domainArr == null || domainArr.Count < 2)
                {
                    return Array.Empty<float>();
                }
                float d0 = domainArr[0].AsFloat();
                float d1 = domainArr[1].AsFloat();
                if (Math.Abs(d1 - d0) < 1e-12f) d1 = d0 + 1f;

                var sizeArr = dict.GetArray(PdfTokens.SizeKey);
                if (sizeArr == null || sizeArr.Count < 1) return Array.Empty<float>();
                int size = Math.Max(1, sizeArr[0].AsInteger());

                int bps = dict.GetInteger("/BitsPerSample"); // literal key for now
                if (bps < 1 || bps > 32) return Array.Empty<float>();

                var rangeArr = dict.GetArray("/Range");
                if (rangeArr == null || rangeArr.Count < 2) return Array.Empty<float>();
                int componentCount = rangeArr.Count / 2;

                var encodeArr = dict.GetArray(PdfTokens.EncodeKey);
                float e0 = 0f, e1 = size - 1;
                if (encodeArr != null && encodeArr.Count >= 2)
                {
                    e0 = encodeArr[0].AsFloat();
                    e1 = encodeArr[1].AsFloat();
                    if (Math.Abs(e1 - e0) < 1e-12f) e1 = e0 + 1f;
                }
                var decodeArr = dict.GetArray(PdfTokens.DecodeKey);

                float clamped = x;
                if (clamped < d0) clamped = d0; else if (clamped > d1) clamped = d1;
                float t = (clamped - d0) / (d1 - d0);
                float u = e0 + t * (e1 - e0);
                if (size == 1) u = 0f;
                if (u < 0f) u = 0f; else if (u > size - 1) u = size - 1;
                int i0 = (int)Math.Floor(u);
                int i1 = i0 + 1; if (i1 >= size) i1 = i0;
                float frac = u - i0;

                var raw = PdfStreamDecoder.DecodeContentStream(funcObj).ToArray();
                if (raw.Length == 0) return Array.Empty<float>();

                int comps = componentCount;
                var br = new BitReader(raw);
                float[,] table = new float[size, comps];
                int maxSample = (bps == 32) ? -1 : ((1 << bps) - 1);
                for (int s = 0; s < size; s++)
                {
                    for (int c = 0; c < comps; c++)
                    {
                        uint sample = br.ReadBits(bps);
                        float norm = (bps == 32) ? sample / 4294967295f : (maxSample > 0 ? sample / (float)maxSample : 0f);
                        float dMin, dMax;
                        if (decodeArr != null && decodeArr.Count >= 2 * comps)
                        {
                            dMin = decodeArr[2 * c].AsFloat();
                            dMax = decodeArr[2 * c + 1].AsFloat();
                        }
                        else
                        {
                            dMin = rangeArr[2 * c].AsFloat();
                            dMax = rangeArr[2 * c + 1].AsFloat();
                        }
                        table[s, c] = dMin + norm * (dMax - dMin);
                    }
                }

                var output = new float[comps];
                for (int c = 0; c < comps; c++)
                {
                    float v0 = table[i0, c];
                    float v1 = table[i1, c];
                    float v = (i0 == i1) ? v0 : v0 + frac * (v1 - v0);
                    float rMin = rangeArr[2 * c].AsFloat();
                    float rMax = rangeArr[2 * c + 1].AsFloat();
                    if (v < rMin) v = rMin; else if (v > rMax) v = rMax;
                    output[c] = v;
                }
                return output;
            }
            catch
            {
                return Array.Empty<float>();
            }
        }

        private static float[] ArrayFromArray(List<IPdfValue> arr)
        {
            var vals = new float[arr.Count];
            for (int i = 0; i < arr.Count; i++) vals[i] = arr[i].AsFloat();
            return vals;
        }

        private sealed class BitReader
        {
            private readonly byte[] _data;
            private int _bitPos;
            public BitReader(byte[] data)
            {
                _data = data ?? Array.Empty<byte>();
                _bitPos = 0;
            }
            public uint ReadBits(int count)
            {
                if (count <= 0 || count > 32) return 0;
                uint value = 0;
                for (int i = 0; i < count; i++)
                {
                    int byteIndex = _bitPos >> 3;
                    if (byteIndex >= _data.Length) break;
                    int shift = 7 - (_bitPos & 7);
                    uint bit = (uint)((_data[byteIndex] >> shift) & 1);
                    value = (value << 1) | bit;
                    _bitPos++;
                }
                return value;
            }
        }
    }
}
