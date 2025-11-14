using PdfReader.Models;
using PdfReader.Rendering.Color.Clut;
using SkiaSharp;
using System;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace PdfReader.Rendering.Color
{
    /// <summary>
    /// Base type for PDF color space converters producing sRGB output.
    /// Implements IDisposable to release cached color filters.
    /// </summary>
    public abstract class PdfColorSpaceConverter : IDisposable
    {
        private const float ToFloat = 1f / 255f;
        private const int MaxByte = 255;

        private readonly ConcurrentDictionary<PdfRenderingIntent, SKColorFilter> _colorFilterCache = new ConcurrentDictionary<PdfRenderingIntent, SKColorFilter>();
        private bool _disposed;

        /// <summary>
        /// Finalizer to ensure unmanaged resources are released.
        /// </summary>
        ~PdfColorSpaceConverter()
        {
            Dispose(false);
        }

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
        /// <returns>A floating-point value between 0 and 1, inclusive, representing the normalized value of <paramref name="b"/>.</returns>
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

        public virtual SKColorFilter AsColorFilter(PdfRenderingIntent intent)
        {
            return _colorFilterCache.GetOrAdd(intent, key => BuldColorFilter(intent));
        }

        protected virtual SKColorFilter BuldColorFilter(PdfRenderingIntent intent)
        {
            return ColorFilterClut.BuildClutColorFilter(
                    ColorFilterClutResolution.Normal,
                    Components,
                    intent,
                    ToSrgbCore);
        }

        /// <summary>
        /// Releases all resources used by the converter, including cached color filters.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources.
        /// </summary>
        /// <param name="disposing">True if called from Dispose; false if called from finalizer.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            // Always dispose unmanaged resources
            foreach (var filterPair in _colorFilterCache)
            {
                filterPair.Value?.Dispose();
            }
            _colorFilterCache.Clear();

            // If disposing, release managed resources here (none currently)

            _disposed = true;
        }
    }
}
