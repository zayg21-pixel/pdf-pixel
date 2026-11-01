using PdfPixel.Commands;
using PdfPixel.Shading;
using PdfPixel.Skia.Cache;
using PdfPixel.Skia.Converters;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawShading(DrawShadingCommand command)
    {
        SkiaColorSampler colorSampler = SkiaCommandUtilities.GetOrCreateColorSampler(_executionContext, command.Paint);
        ShadingCommandCacheEntry entry = GetOrBuildShadingEntry(command.Content, colorSampler);

        switch (command.Content.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                ExecuteShadingFunctionBased(command, entry);
                break;
            }
            case PdfShadingType.Axial:
            {
                ExecuteShadingAxial(command, entry);
                break;
            }
            case PdfShadingType.Radial:
            {
                ExecuteShadingRadial(command, entry);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                ExecuteShadingMesh(command, entry);
                break;
            }
        }
    }

    /// <summary>
    /// Returns the cached primitives built for the shading content with its colors converted by
    /// <paramref name="colorSampler"/>, building and storing them when the cache holds none.
    /// </summary>
    private ShadingCommandCacheEntry GetOrBuildShadingEntry(PdfShadingContent content, SkiaColorSampler colorSampler)
    {
        ShadingCommandCacheKey key = new(content, colorSampler);

        if (_executionContext.Cache.GetEntry(key) is ShadingCommandCacheEntry existing)
        {
            return existing;
        }

        SKImage? image = (content.Function != null) ? PdfShadingConverter.ToSkImage(content.Function, colorSampler) : null;
        SKShader? shader = null;
        SKShader? innerShader = null;

        if (content.Axial != null)
        {
            shader = content.Axial.ToSkiaShader(colorSampler);
        }
        else if (content.Radial != null)
        {
            shader = content.Radial.ToSkiaOuterShader(colorSampler);
            innerShader = content.Radial.ToSkiaInnerShader(colorSampler);
        }

        SKVertices? vertices = content.Mesh?.ToSkVertices(colorSampler);

        ShadingCommandCacheEntry entry = new(image, shader, innerShader, vertices);
        _executionContext.Cache.StoreEntry(key, entry);
        return entry;
    }

    private void ExecuteShadingFunctionBased(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Image == null || command.Content.Function == null)
        {
            return;
        }

        _canvas.Save();
        _canvas.Concat(command.Content.Function.Matrix.ToSkMatrix());
        _canvas.DrawImage(entry.Image, SKPoint.Empty, SKSamplingOptions.Default);
        _canvas.Restore();
    }

    private void ExecuteShadingAxial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Shader == null)
        {
            return;
        }

        DrawShadingShaderToCanvas(command, entry.Shader);
    }

    private void ExecuteShadingRadial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Shader == null || entry.InnerShader == null)
        {
            return;
        }

        DrawShadingShaderToCanvas(command, entry.InnerShader);
        DrawShadingShaderToCanvas(command, entry.Shader);
    }

    private void ExecuteShadingMesh(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Vertices == null)
        {
            return;
        }

        using SKPaint paint = new()
        {
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.Paint.Alpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.DrawVertices(entry.Vertices, SKBlendMode.DstIn, paint);
    }

    private void DrawShadingShaderToCanvas(DrawShadingCommand command, SKShader shader)
    {
        using SKPaint paint = new()
        {
            Shader = shader,
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.Paint.Alpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.DrawPaint(paint);
    }
}
