using SkiaSharp;
using System;

namespace PdfReader.Color.Filters;

/// <summary>
/// Provides Skia color filter-based color conversion for indexed color spaces using a palette texture.
/// </summary>
internal static class IndexedColorFilter
{
    /// <summary>
    /// Builds an <see cref="SKColorFilter"/> for indexed color spaces using a palette texture and a runtime shader.
    /// </summary>
    /// <param name="palette">The palette of <see cref="SKColor"/> values representing the indexed color table.</param>
    /// <returns>
    /// An <see cref="SKColorFilter"/> that maps grayscale input (where the R channel is the palette index in [0,1])
    /// to the corresponding color from the palette. The filter uses a shader to perform the lookup efficiently.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="palette"/> is null.</exception>
    /// <exception cref="InvalidOperationException">Thrown if the shader cannot be compiled.</exception>
    public static SKColorFilter BuildIndexedColorFilter(SKColor[] palette)
    {
        if (palette == null)
        {
            throw new ArgumentNullException(nameof(palette));
        }

        int paletteSize = palette.Length;

        using SKBitmap paletteBitmap = new SKBitmap(paletteSize, 1, SKColorType.Rgba8888, SKAlphaType.Premul);
        paletteBitmap.Pixels = palette;

        // Only clamp, do not divide by paletteSize
        string shaderSource = @"
                uniform shader palette;
                uniform float paletteSize;
                half4 main(half4 inColor) {
                    float idx = clamp(inColor.r * 255.0, 0.0, paletteSize - 1.0);
                    float texCoord = (idx + 0.5);
                    return palette.eval(float2(texCoord, 0.5));
                }
            ";

        var effect = SKRuntimeEffect.CreateColorFilter(shaderSource, out var error);
        if (effect == null)
        {
            throw new InvalidOperationException($"Failed to compile indexed palette shader: {error}");
        }

        var uniforms = new SKRuntimeEffectUniforms(effect)
        {
            ["paletteSize"] = (float)paletteSize
        };

        var children = new SKRuntimeEffectChildren(effect)
        {
            { "palette", ColorFilterTexture.ToLutShader(paletteBitmap) }
        };

        return effect.ToColorFilter(uniforms, children);
    }
}
