using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Provides a Skia color filter that converts CMYK (represented as RGBA: R=C, G=M, B=Y, A=K) to sRGB using a runtime shader.
    /// </summary>
    internal static class DeviceCmykColorFilter
    {
        /// <summary>
        /// Builds an <see cref="SKColorFilter"/> that converts CMYK (as RGBA) to sRGB.
        /// </summary>
        /// <returns>
        /// An <see cref="SKColorFilter"/> that maps input CMYK (R=C, G=M, B=Y, A=K, all in [0,1]) to sRGB output.
        /// </returns>
        public static SKColorFilter BuildDeviceCmykColorFilter()
        {
            // Standard CMYK to sRGB conversion (PDF spec: C' = C * (1-K) + K, then R = 1 - C', etc.)
            string shaderSource = @"
                half4 main(half4 cmyk) {
                    float c = cmyk.r;
                    float m = cmyk.g;
                    float y = cmyk.b;
                    float k = cmyk.a;
                    float r = 1.0 - (c * (1.0 - k) + k);
                    float g = 1.0 - (m * (1.0 - k) + k);
                    float b = 1.0 - (y * (1.0 - k) + k);
                    return half4(r, g, b, 1.0);
                }
            ";

            var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile DeviceCmykColorFilter shader: {error}");
            }

            return effect.ToColorFilter();
        }
    }
}
