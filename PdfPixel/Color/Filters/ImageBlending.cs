using SkiaSharp;

namespace PdfPixel.Color.Filters
{
    /// <summary>
    /// Provides SKSL-based image blending with mask, matte, and optional alpha inversion.
    /// </summary>
    internal static class ImageBlending
    {
        private static readonly SKRuntimeEffect _softMaskEffect;
        private static readonly SKRuntimeEffect _imageMaskEffect;

        static ImageBlending()
        {
            var softMaskSksl = @"
                uniform shader image;
                uniform shader mask;
                uniform half3 matte;
                uniform half hasMatte;

                half4 main(float2 coord) {
                    half3 color = image.eval(coord).rgb;
                    half alpha = mask.eval(coord).r;

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

            var imageMaskSksl = @"
                uniform shader mask;
                uniform half3 fillColor;
                uniform half useInverse;

                half4 main(float2 coord) {
                    half gray = mask.eval(coord).r;
                    half maskAlpha = mix(gray, 1.0 - gray, useInverse);
                    return half4(fillColor * maskAlpha, maskAlpha);
                }
            ";

            _softMaskEffect = SKRuntimeEffect.CreateShader(softMaskSksl, out _);
            _imageMaskEffect = SKRuntimeEffect.CreateShader(imageMaskSksl, out _);
        }

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
            var uniforms = new SKRuntimeEffectUniforms(_softMaskEffect)
            {
                ["hasMatte"] = matte.HasValue ? 1.0f : 0.0f,
                ["matte"] = matte ?? default,
            };

            var children = new SKRuntimeEffectChildren(_softMaskEffect)
            {
                { "image", image.ToRawShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SKMatrix.CreateScale(1 / (float)image.Width, 1 / (float)image.Height)) },
                { "mask", mask.ToRawShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SKMatrix.CreateScale(1 / (float)mask.Width, 1 / (float)mask.Height)) }
            };

            return _softMaskEffect.ToShader(uniforms, children);
        }

        /// <summary>
        /// Creates an <see cref="SKShader"/> that renders a stencil image mask using the given fill color.
        /// Handles gray-to-alpha conversion, optional inversion (decode array logic),
        /// and applies fill color and fill alpha, producing a premultiplied output.
        /// </summary>
        /// <param name="mask">The mask <see cref="SKImage"/> (grayscale; red channel used as mask alpha).</param>
        /// <param name="fillColor">The fill color to paint where the mask is opaque.</param>
        /// <param name="fillAlpha">The fill alpha (0..1) to modulate the final output alpha.</param>
        /// <param name="inverse">If <see langword="true"/>, inverts the mask alpha (1 - gray).</param>
        /// <param name="sampling">Sampling options for the mask image.</param>
        /// <returns>An <see cref="SKShader"/> that composites the stencil mask with fill color and fill alpha.</returns>
        public static SKShader CreateImageMaskBlendingShader(
            SKImage mask,
            SKColor fillColor,
            bool inverse,
            SKSamplingOptions sampling)
        {
            var uniforms = new SKRuntimeEffectUniforms(_imageMaskEffect)
            {
                ["fillColor"] = fillColor,
                ["useInverse"] = inverse ? 1.0f : 0.0f,
            };

            var children = new SKRuntimeEffectChildren(_imageMaskEffect)
            {
                { "mask", mask.ToRawShader(SKShaderTileMode.Repeat, SKShaderTileMode.Repeat, sampling, SKMatrix.CreateScale(1 / (float)mask.Width, 1 / (float)mask.Height)) }
            };

            return _imageMaskEffect.ToShader(uniforms, children);
        }
    }
}
