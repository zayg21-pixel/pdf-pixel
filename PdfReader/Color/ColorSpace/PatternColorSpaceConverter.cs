using System;
using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Color.ColorSpace
{
    /// <summary>
    /// Converter placeholder for PDF Pattern color space.
    /// Colored patterns: no base color space, components = 0 (color defined by pattern content stream).
    /// Uncolored (stencil) tiling patterns: pattern provides shape, current color supplied via base color space components.
    /// This converter only acts as a marker; actual pattern painting handled by pattern painting logic.
    /// </summary>
    internal sealed class PatternColorSpaceConverter : PdfColorSpaceConverter
    {
        private readonly PdfColorSpaceConverter _baseColorSpace;

        public PatternColorSpaceConverter(PdfColorSpaceConverter baseColorSpace)
        {
            _baseColorSpace = baseColorSpace; // may be null for colored patterns
        }

        public bool IsUncolored => _baseColorSpace != null;

        public PdfColorSpaceConverter BaseColorSpace => _baseColorSpace;

        public override bool IsDevice => false;

        public override int Components => _baseColorSpace?.Components ?? 0;

        protected override SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
        {
            // Pattern space itself cannot directly produce a color without pattern painting.
            // For uncolored patterns, we interpret provided components through base color space to get the current color.
            if (_baseColorSpace != null)
            {
                return _baseColorSpace.ToSrgb(comps01, intent);
            }

            // Colored pattern color should be determined during pattern painting; default to black placeholder.
            return SKColors.Black;
        }
    }
}
