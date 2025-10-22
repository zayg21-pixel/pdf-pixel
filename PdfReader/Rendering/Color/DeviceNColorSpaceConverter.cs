using System;
using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Simplified DeviceN color space converter.
    /// Maps N-component tint array through a tint transform function into an alternate color space.
    /// According to PDF spec, /DeviceN [/Names] /AltCS /TintTransform.
    /// We currently only support non-PostScript tint functions (types 0,2,3) via PdfFunctions.
    /// </summary>
    internal sealed class DeviceNColorSpaceConverter : PdfColorSpaceConverter
    {
        private readonly string[] _componentNames;
        private readonly PdfColorSpaceConverter _alternate;
        private readonly PdfFunction _tintFunction;

        public DeviceNColorSpaceConverter(string[] componentNames, PdfColorSpaceConverter alternate, PdfFunction tintFunction)
        {
            _componentNames = componentNames ?? Array.Empty<string>();
            _alternate = alternate ?? DeviceRgbConverter.Instance;
            _tintFunction = tintFunction;
        }

        public override int Components => _componentNames.Length > 0 ? _componentNames.Length : 1;

        public override bool IsDevice => false;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
        {
            ReadOnlySpan<float> mapped = comps01;
            if (_tintFunction != null)
            {
                mapped = _tintFunction.Evaluate(comps01);
                if (mapped == null || mapped.Length == 0)
                {
                    mapped = comps01;
                }
            }

            return _alternate.ToSrgb(mapped, intent);
        }
    }
}
