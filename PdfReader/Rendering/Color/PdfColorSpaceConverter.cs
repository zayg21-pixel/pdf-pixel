using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Base type for PDF color space converters producing sRGB output.
    /// Implementations convert component values (normalized 0..1 floats or 8-bit samples) to sRGB.
    /// Provides per-row 8-bit batch conversion and a float based conversion entry point.
    /// </summary>
    public abstract class PdfColorSpaceConverter
    {
        private const float ToFloat = 1f / 255f;
        private const int MaxByte = 255;

        private readonly ConcurrentDictionary<PdfRenderingIntent, IRgbaSampler> _rgbaLutCache = new ConcurrentDictionary<PdfRenderingIntent, IRgbaSampler>();
        private static readonly DeviceRgbaSampler _defaultSampler = new DeviceRgbaSampler();

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
            if (IsDevice)
            {
                return ToSrgbCore(comps01, intent);
            }
            var rgba = new Rgba();

            switch (Components)
            {
                case 1:
                    {
                        byte value = ToByte(comps01[0]);
                        rgba.R = value;
                        rgba.G = value;
                        rgba.B = value;
                        rgba.A = MaxByte;
                        break;
                    }
                case 3:
                    {
                        rgba.R = ToByte(comps01[0]);
                        rgba.G = ToByte(comps01[1]);
                        rgba.B = ToByte(comps01[2]);
                        rgba.A = MaxByte;
                        break;
                    }
                case 4:
                    {
                        rgba.R = ToByte(comps01[0]);
                        rgba.G = ToByte(comps01[1]);
                        rgba.B = ToByte(comps01[2]);
                        rgba.A = ToByte(comps01[3]);
                        break;
                    }
            }

            var sampler = GetSampler(intent);
            
            sampler.Sample(ref rgba, ref rgba);

            return new SKColor(rgba.R, rgba.G, rgba.B, rgba.A);
        }

        internal virtual IRgbaSampler GetSampler(PdfRenderingIntent intent)
        {
            if (IsDevice)
            {
                return _defaultSampler;
            }

            switch (Components)
            {
                case 4:
                    return _rgbaLutCache.GetOrAdd(intent, ri => LayeredThreeDLut.Build(intent, ToSrgbCore));
                default:
                    return _rgbaLutCache.GetOrAdd(intent, ri => TreeDLut.Build(intent, ToSrgbCore));
            }
        }

        /// <summary>
        /// Clamps a value to the 0..1 interval.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static float ToFloat01(byte b)
        {
            return b * ToFloat;
        }

        /// <summary>
        /// Converts a normalized value (0..1) to an 8-bit unsigned byte with rounding.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static byte ToByte(float v01)
        {
            var value = v01 * MaxByte + 0.5f;

            if (value <= 0f)
            {
                return 0;
            }

            if (value >= MaxByte)
            {
                return MaxByte;
            }

            return (byte)value;
        }


        private sealed class DeviceRgbaSampler : IRgbaSampler
        {
            public bool IsDefault => true;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void Sample(ref Rgba source, ref Rgba destination)
            {
                // no op
            }
        }
    }
}
