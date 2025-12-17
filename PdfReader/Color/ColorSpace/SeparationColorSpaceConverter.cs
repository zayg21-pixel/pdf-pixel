using PdfReader.Color.Lut;
using PdfReader.Color.Structures;
using PdfReader.Functions;
using PdfReader.Models;
using SkiaSharp;
using System;
using System.Linq;
using System.Runtime.CompilerServices;

namespace PdfReader.Color.ColorSpace;

/// <summary>
/// Simplified Separation color space converter.
/// Maps single-component tint value through a tint transform function into a base color space.
/// If no function/base is present falls back to DeviceGray.
/// </summary>
internal sealed class SeparationColorSpaceConverter : PdfColorSpaceConverter
{
    private readonly PdfString _name;
    private readonly PdfColorSpaceConverter _alternate;
    private readonly PdfFunction _tintFunction;

    public SeparationColorSpaceConverter(PdfString name, PdfColorSpaceConverter alternate, PdfFunction tintFunction)
    {
        _name = name;
        _alternate = alternate ?? DeviceGrayConverter.Instance;
        _tintFunction = tintFunction;
    }

    public override int Components => 1;

    public override bool IsDevice => false;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
    {
        float tint = comps01.Length > 0 ? comps01[0] : 0f;
        ReadOnlySpan<float> mapped = comps01;

        if (_tintFunction != null)
        {
            mapped = _tintFunction.Evaluate(tint);

            if (mapped == null || mapped.Length == 0)
            {
                mapped = comps01;
            }
        }

        return _alternate.GetRgbaSampler(intent).SampleColor(mapped);
    }

    protected override IRgbaSampler GetRgbaSamplerCore(PdfRenderingIntent intent)
    {
        var alternateSampler = _alternate.GetRgbaSampler(intent);

        if (_tintFunction == null)
        {
            return alternateSampler;
        }

        return new SeparationSampler(_tintFunction, alternateSampler);
    }

    internal sealed class SeparationSampler : IRgbaSampler
    {
        private readonly PdfFunction _tintFunction;
        private readonly IRgbaSampler _alternateSampler;
        private readonly RgbaPacked[] _tintLut;
        private const int DefaultLutSize = 256;

        public SeparationSampler(PdfFunction tintFunction, IRgbaSampler alternateSampler)
        {
            _tintFunction = tintFunction;
            _alternateSampler = alternateSampler;
            
            // Build tint LUT if we have a tint function
            if (_tintFunction != null)
            {
                _tintLut = BuildTintLut(_tintFunction, _alternateSampler, DefaultLutSize);
            }
        }

        public bool IsDefault => false;

        public void Sample(ReadOnlySpan<float> source, ref RgbaPacked destination)
        {
            // Direct LUT lookup - no function evaluation or alternate sampler calls
            float tint = source[0];
            int index = (int)(tint * (DefaultLutSize - 1) + 0.5f); // Round to nearest
            index = Math.Max(0, Math.Min(DefaultLutSize - 1, index));
            destination = _tintLut[index];
        }

        public SKColor SampleColor(ReadOnlySpan<float> source)
        {
            RgbaPacked packed = default;
            Sample(source, ref packed);
            return new SKColor(packed.R, packed.G, packed.B);
        }

        /// <summary>
        /// Builds a tint LUT with pre-computed RgbaPacked results for 0-1 range.
        /// </summary>
        private static RgbaPacked[] BuildTintLut(PdfFunction tintFunction, IRgbaSampler alternateSampler, int lutSize)
        {
            var lut = new RgbaPacked[lutSize];
            
            for (int i = 0; i < lutSize; i++)
            {
                float tint = (float)i / (lutSize - 1);
                var mappedComponents = tintFunction.Evaluate(tint);
                
                // Pre-compute the final RgbaPacked value
                alternateSampler.Sample(mappedComponents, ref lut[i]);
            }
            
            return lut;
        }
    }
}
