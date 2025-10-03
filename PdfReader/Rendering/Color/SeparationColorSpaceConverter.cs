using System;
using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Simplified Separation color space converter.
    /// Maps single-component tint value through an optional tint transform function (function types 0,2,3) into a base color space.
    /// If no function/base is present falls back to DeviceGray.
    /// </summary>
    internal sealed class SeparationColorSpaceConverter : PdfColorSpaceConverter
    {
        private readonly string _colorantName;
        private readonly PdfColorSpaceConverter _alternate;
        private readonly PdfObject _tintFunction;

        public SeparationColorSpaceConverter(string colorantName, PdfColorSpaceConverter alternate, PdfObject tintFunction)
        {
            _colorantName = colorantName;
            _alternate = alternate ?? DeviceGrayConverter.Instance;
            _tintFunction = tintFunction;
        }

        public override int Components => 1;

        public override bool IsDevice => false;

        public override SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
        {
            float tint = comps01.Length > 0 ? comps01[0] : 0f;
            float[] mapped;
            if (_tintFunction != null)
            {
                mapped = PdfFunctions.EvaluateFunctionObject(_tintFunction, Clamp01(tint));
                if (mapped == null || mapped.Length == 0)
                {
                    mapped = new[] { Clamp01(1f - tint) }; // fallback like simple subtractive
                }
            }
            else
            {
                mapped = new[] { Clamp01(1f - tint) }; // simple heuristic mapping
            }
            return _alternate.ToSrgb(mapped, intent);
        }
    }
}
