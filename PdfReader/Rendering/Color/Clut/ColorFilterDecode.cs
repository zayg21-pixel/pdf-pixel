using SkiaSharp;
using System;

namespace PdfReader.Rendering.Color.Clut
{
    /// <summary>
    /// Provides Skia color filters for PDF decode mapping per channel and for image masks.
    /// Supports Grayscale (1 channel), RGB (3 channels), and CMYK (4 channels).
    /// Leaves alpha channel untouched except for mask images.
    /// </summary>
    internal class ColorFilterDecode
    {
        /// <summary>
        /// Builds a decode color filter for the specified number of channels (not for masks).
        /// </summary>
        /// <param name="decode">Decode array (2 values per channel).</param>
        /// <param name="channelCount">Number of color channels (1, 3, or 4).</param>
        /// <returns>SKColorFilter that applies decode mapping.</returns>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if channelCount is not 1, 3, or 4.</exception>
        public static SKColorFilter BuildDecodeColorFilter(float[] decode, int channelCount)
        {
            if (channelCount != 1 && channelCount != 3 && channelCount != 4)
            {
                throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only 1, 3, or 4 channels are supported.");
            }

            if (decode == null || decode.Length != channelCount * 2)
            {
                throw new ArgumentException("Decode array must have 2 values per channel.", nameof(decode));
            }

            string shaderSource = BuildShaderSource(channelCount);
            var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile decode shader: {error}");
            }

            var uniforms = new SKRuntimeEffectUniforms(effect);
            for (int i = 0; i < channelCount; i++)
            {
                uniforms[$"decodeMin{i}"] = decode[i * 2];
                uniforms[$"decodeMax{i}"] = decode[i * 2 + 1];
            }

            return effect.ToColorFilter(uniforms);
        }

        /// <summary>
        /// Builds a Skia color filter for PDF image masks, applying luminosity-to-alpha conversion and decode mapping as specified by the PDF specification and legacy pipeline.
        /// The filter computes alpha from the input color's luminosity (for RGB) or value (for Gray), then applies the decode mapping and inversion logic.
        /// The output is always black (RGB = 0) with the computed alpha channel, suitable for use with image masks and soft masks.
        /// </summary>
        /// <param name="decode">
        /// The decode array for the mask channel (2 values). If null or invalid, uses [0, 1] (identity).
        /// </param>
        /// <returns>
        /// An <see cref="SKColorFilter"/> that applies luminosity-to-alpha and mask decode logic to the alpha channel, outputting black for RGB.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the SKSL shader fails to compile.
        /// </exception>
        public static SKColorFilter BuildMaskDecodeFilter(float[] decode)
        {
            float[] decodeArray = decode;
            if (decode == null || decode.Length < 2)
            {
                decodeArray = new float[] { 0f, 1f };
            }

            float decodeMin = decodeArray[1];
            float decodeMax = decodeArray[0];

            // SKSL: Compute luminosity for RGB, or use value for Gray
            string shaderSource = @"
                uniform float decodeMin0;
                uniform float decodeMax0;
                half4 main(half4 color) {
                    half lum = 0.299 * color.r + 0.587 * color.g + 0.114 * color.b;
                    half decoded = decodeMin0 + (decodeMax0 - decodeMin0) * lum;
                    return half4(0, 0, 0, decoded);
                }
            ";

            var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile mask decode shader: {error}");
            }

            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["decodeMin0"] = decodeMin,
                ["decodeMax0"] = decodeMax
            };

            return effect.ToColorFilter(uniforms);
        }

        private static string BuildShaderSource(int channelCount)
        {
            if (channelCount == 1)
            {
                // Grayscale
                return @"
                    uniform float decodeMin0;
                    uniform float decodeMax0;
                    half4 main(half4 color) {
                        half gray = color.r;
                        half decoded = decodeMin0 + (decodeMax0 - decodeMin0) * gray;
                        return half4(decoded, decoded, decoded, color.a);
                    }
                ";
            }
            else if (channelCount == 3)
            {
                // RGB
                return @"
                    uniform float decodeMin0;
                    uniform float decodeMax0;
                    uniform float decodeMin1;
                    uniform float decodeMax1;
                    uniform float decodeMin2;
                    uniform float decodeMax2;
                    half4 main(half4 color) {
                        half r = decodeMin0 + (decodeMax0 - decodeMin0) * color.r;
                        half g = decodeMin1 + (decodeMax1 - decodeMin1) * color.g;
                        half b = decodeMin2 + (decodeMax2 - decodeMin2) * color.b;
                        return half4(r, g, b, color.a);
                    }
                ";
            }
            else if (channelCount == 4)
            {
                // CMYK (pass through, decode each channel, conversion to RGB should be done in next filter)
                return @"
                    uniform float decodeMin0;
                    uniform float decodeMax0;
                    uniform float decodeMin1;
                    uniform float decodeMax1;
                    uniform float decodeMin2;
                    uniform float decodeMax2;
                    uniform float decodeMin3;
                    uniform float decodeMax3;
                    half4 main(half4 color) {
                        half c = decodeMin0 + (decodeMax0 - decodeMin0) * color.r;
                        half m = decodeMin1 + (decodeMax1 - decodeMin1) * color.g;
                        half y = decodeMin2 + (decodeMax2 - decodeMin2) * color.b;
                        half k = decodeMin3 + (decodeMax3 - decodeMin3) * color.a;
                        return half4(c, m, y, k);
                    }
                ";
            }
            else
            {
                throw new ArgumentOutOfRangeException(nameof(channelCount), channelCount, "Only 1, 3, or 4 channels are supported.");
            }
        }
    }
}
