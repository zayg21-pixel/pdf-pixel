using SkiaSharp;

namespace PdfPixel.Imaging.Processing
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
            const string softMaskSksl = @"
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

            const string imageMaskSksl = @"
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

        public static SKShader BuildImageShader(
            SKImage source,
            SKSizeI targetSize,
            in SKSamplingOptions sampling)
        {
            return source.ToShader(
                SKShaderTileMode.Clamp,
                SKShaderTileMode.Clamp,
                sampling,
                BuildShaderMatrix(source, targetSize));
        }

        private static SKMatrix BuildShaderMatrix(SKImage source, SKSizeI targetSize)
        {
            return SKMatrix.CreateScale(
                (float)targetSize.Width / source.Width,
                (float)targetSize.Height / source.Height);
        }

        /// <summary>
        /// Creates a soft-mask blending shader from pre-built image and mask child shaders.
        /// Both children are expected to interpret the runtime effect's <c>coord</c> consistently —
        /// the caller is responsible for applying any local-matrix offsets needed for tile alignment.
        /// </summary>
        public static SKShader CreateSoftMaskBlendingShader(
            SKShader imageChild,
            SKShader maskChild,
            SKColor? matte)
        {
            SKRuntimeEffectUniforms uniforms = new(_softMaskEffect)
            {
                ["hasMatte"] = (matte.HasValue) ? 1.0f : 0.0f,
                ["matte"] = matte ?? default
            };

            SKRuntimeEffectChildren children = new(_softMaskEffect)
            {
                { "image", imageChild },
                { "mask", maskChild }
            };

            return _softMaskEffect.ToShader(uniforms, children);
        }

        /// <summary>
        /// Creates an image-mask blending shader from a pre-built mask child shader.
        /// The mask child is the stencil image itself (carrying any tile offset in its local matrix).
        /// </summary>
        public static SKShader CreateImageMaskBlendingShader(
            SKShader maskChild,
            in SKColor fillColor,
            bool inverse)
        {
            SKRuntimeEffectUniforms uniforms = new(_imageMaskEffect)
            {
                ["fillColor"] = fillColor,
                ["useInverse"] = inverse ? 1.0f : 0.0f
            };

            SKRuntimeEffectChildren children = new(_imageMaskEffect) { { "mask", maskChild } };

            return _imageMaskEffect.ToShader(uniforms, children);
        }
    }
}
