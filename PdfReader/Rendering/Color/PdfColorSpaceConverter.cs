using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;
using System.Collections.Concurrent;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Base type for PDF color space converters producing sRGB output.
    /// Implementations convert component values (normalized 0..1 floats or 8-bit samples) to sRGB.
    /// Provides per-row 8-bit batch conversion and a float based conversion entry point.
    /// </summary>
    public abstract class PdfColorSpaceConverter
    {
        private readonly ConcurrentDictionary<PdfRenderingIntent, byte[]> _grayLutCache = new ConcurrentDictionary<PdfRenderingIntent, byte[]>();
        private readonly ConcurrentDictionary<PdfRenderingIntent, byte[]> _rgbLutCache = new ConcurrentDictionary<PdfRenderingIntent, byte[]>();
        private readonly ConcurrentDictionary<PdfRenderingIntent, LayeredThreeDLut> _cmykLutCache = new ConcurrentDictionary<PdfRenderingIntent, LayeredThreeDLut>();

        /// <summary>
        /// Gets the number of input components for the color space (e.g. 1=Gray, 3=RGB, 4=CMYK).
        /// </summary>
        public abstract int Components { get; }

        /// <summary>
        /// Gets a value indicating whether this converter represents a device (native) color space.
        /// Device spaces may bypass certain lookups.
        /// </summary>
        public abstract bool IsDevice { get; }

        /// <summary>
        /// Core float (0..1) component to sRGB conversion implemented by derived types.
        /// </summary>
        /// <param name="comps01">Component values in the range 0..1.</param>
        /// <param name="intent">Rendering intent to apply.</param>
        /// <returns>Converted sRGB color.</returns>
        protected abstract SKColor ToSrgbCore(ReadOnlySpan<float> comps01, PdfRenderingIntent intent);

        /// <summary>
        /// Converts normalized (0..1) component values to sRGB using the derived converter implementation.
        /// </summary>
        /// <param name="comps01">Component values in the range 0..1.</param>
        /// <param name="intent">Rendering intent.</param>
        /// <returns>sRGB color.</returns>
        public virtual SKColor ToSrgb(ReadOnlySpan<float> comps01, PdfRenderingIntent intent)
        {
            return ToSrgbCore(comps01, intent);
        }

        public virtual unsafe void Sample8RgbaInPlace(byte* rgbaRow, int pixelCount, PdfRenderingIntent intent)
        {
            if (Components == 1)
            {
                byte[] grayLut = _grayLutCache.GetOrAdd(intent, ri => OneDLut.Build8Bit(ri, ToSrgbCore));
                fixed (byte* pLut = grayLut)
                {
                    OneDLut.Sample8RgbaInPlace(pLut, rgbaRow, pixelCount);
                }
                return;
            }

            if (Components == 3)
            {
                byte[] lut = _rgbLutCache.GetOrAdd(intent, ri => TreeDLut.Build8Bit(ri, ToSrgbCore));
                fixed (byte* pLut = lut)
                {
                    TreeDLut.SampleBilinear8RgbaInPlace(pLut, rgbaRow, pixelCount);
                }
                return;
            }

            if (Components == 4)
            {
                LayeredThreeDLut layered = _cmykLutCache.GetOrAdd(intent, ri => LayeredThreeDLut.Build(ri, ToSrgbCore));
                layered.SampleRgbaInPlace(rgbaRow, pixelCount);
                return;
            }
            // Unsupported component count - do nothing.
        }

        /// <summary>
        /// Clamps a value to the 0..1 interval.
        /// </summary>
        protected static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }

        /// <summary>
        /// Converts an 8-bit unsigned integer to a normalized floating-point value in the range [0, 1].
        /// </summary>
        /// <param name="b">The 8-bit unsigned integer to convert.</param>
        /// <returns>A floating-point value between 0 and 1, inclusive, representing the normalized value of <paramref
        /// name="b"/>.</returns>
        protected static float ToFloat01(byte b)
        {
            return OneDLut.ByteToFloat01[b];
        }

        /// <summary>
        /// Converts a normalized value (0..1) to an 8-bit unsigned byte with rounding.
        /// </summary>
        protected static byte ToByte(float v01)
        {
            return OneDLut.ToByte(v01);
        }
    }
}
