using SkiaSharp;
using System;

namespace PdfPixel.Color.Filters
{
    /// <summary>
    /// Provides SKSL-based image blending with mask, matte, and optional alpha inversion.
    /// </summary>
    internal static class ImageBlending
    {
        /// <summary>
        /// Creates an <see cref="SKShader"/> that blends an image with a mask and matte color.
        /// </summary>
        /// <param name="image">The source <see cref="SKImage"/>.</param>
        /// <param name="mask">The mask <see cref="SKImage"/> (alpha channel used).</param>
        /// <param name="matte">The matte <see cref="SKColor"/> to use for dematting.</param>
        /// <param name="inverseAlpha">If true, inverts the mask alpha.</param>
        /// <param name="sampling">Sampling options for both images.</param>
        /// <returns>An <see cref="SKShader"/> that blends the image and mask with matte and alpha options.</returns>
        public static SKShader CreateSoftMaskBlendingShader(
            SKImage image,
            SKImage mask,
            SKColor? matte,
            SKSamplingOptions sampling)
        {
            const string sksl = @"
                uniform shader image;
                uniform shader mask;
                uniform half3 matte;
                uniform half hasMatte;

                half4 main(float2 coord) {
                    half3 color = image.eval(coord).rgb;
                    half alpha = mask.eval(coord).r;

                    //alpha= mix(alpha, 1.0 - alpha, inverseAlpha);

                    if (hasMatte == 0.0)
                    {
                        return half4(color * alpha, alpha);
                    }
                    else
                    {
                        color = matte - matte / alpha + color; // since we don't multiply color in advance, this is correct formula for getting dematte effect
                        return half4(color, alpha);
                    }
                }
            ";

            var effect = SKRuntimeEffect.CreateShader(sksl, out var error);

            if (effect == null)
            {
                throw new InvalidOperationException($"Failed to compile ImageBlending shader: {error}");
            }

            var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["hasMatte"] = matte.HasValue ? 1.0f : 0.0f,
                ["matte"] = matte ?? default,
            };

            var children = new SKRuntimeEffectChildren(effect)
            {
                { "image", image.ToRawShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SKMatrix.CreateScale(1 / (float)image.Width, 1 / (float)image.Height)) },
                { "mask", mask.ToRawShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SKMatrix.CreateScale(1 / (float)mask.Width, 1 / (float)mask.Height)) }
            };

            return effect.ToShader(uniforms, children);
        }
    }
}
