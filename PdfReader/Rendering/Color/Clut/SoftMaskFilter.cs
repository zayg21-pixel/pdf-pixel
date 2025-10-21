using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Provides Skia color filter-based alpha masking using a mask array and PDF dematting algorithm.
    /// </summary>
    internal static class SoftMaskFilter
    {
        /// <summary>
        /// Builds an <see cref="SKColorFilter"/> that applies an alpha mask based on the provided mask array.
        /// The filter maps the grayscale input (R channel) to alpha values according to the mask.
        /// </summary>
        /// <param name="mask">The mask array, where each value represents an allowed sample value (0-255).</param>
        /// <returns>
        /// An <see cref="SKColorFilter"/> that sets alpha to 1.0 if the input value is in the mask, otherwise 0.0.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="mask"/> is null.</exception>
        public static SKColorFilter BuildMaskColorFilter(int[] mask)
        {
            if (mask == null)
            {
                throw new ArgumentNullException(nameof(mask));
            }

            byte[] alphaLut = new byte[256];
            for (int value = 0; value < 256; value++)
            {
                alphaLut[value] = (byte)(Array.IndexOf(mask, value) >= 0 ? 255 : 0);
            }

            SKColor[] maskPixels = new SKColor[256];
            for (int i = 0; i < 256; i++)
            {
                maskPixels[i] = new SKColor(0, 0, 0, alphaLut[i]);
            }

            using SKBitmap maskBitmap = new SKBitmap(256, 1, SKColorType.Alpha8, SKAlphaType.Premul);
            maskBitmap.Pixels = maskPixels;

            string shaderSource = @"
                uniform shader maskLut;
                half4 main(half4 inColor) {
                    float idx = clamp(inColor.r * 255.0, 0.0, 255.0);
                    float texCoord = idx + 0.5;
                    half alpha = maskLut.eval(float2(texCoord, 0.5)).a;
                    return half4(inColor.rgb, alpha);
                }
            ";

            var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile SoftMaskFilter shader: {error}");
            }

            var children = new SKRuntimeEffectChildren(effect)
            {
                { "maskLut", maskBitmap.ToShader() }
            };

            return effect.ToColorFilter(null, children);
        }

        /// <summary>
        /// Builds an <see cref="SKColorFilter"/> that applies PDF 2.0 dematting algorithm (§11.9.6) using the specified matte color.
        /// </summary>
        /// <param name="matte">The matte <see cref="SKColor"/> to use for dematting.</param>
        /// <returns>
        /// An <see cref="SKColorFilter"/> that removes the matte effect from each pixel according to the PDF spec.
        /// </returns>
        /// <exception cref="InvalidOperationException">Thrown if the shader cannot be compiled.</exception>
        public static SKColorFilter BuildDematteColorFilter(SKColor matte)
        {
            // Pass matte color as uniform
            string shaderSource = @"
                uniform half3 matte;
                half4 main(half4 color) {
                    float alpha = color.a * 255.0;
                    half3 result;
                    if (alpha == 0.0) {
                        result = matte;
                    } else {
                        result = matte + (color.rgb * 255.0 - matte) * 255.0 / alpha;
                    }
                    result = clamp(result, 0.0, 255.0) / 255.0;
                    return half4(result, 1.0);
                }
            ";

            var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile DematteColorFilter shader: {error}");
            }

            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["matte"] = new SKColor(matte.Red, matte.Green, matte.Blue, 255) // matte as float3
            };

            return effect.ToColorFilter(uniforms);
        }
    }
}
