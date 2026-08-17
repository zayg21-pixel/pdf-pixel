using PdfPixel.Commands;
using PdfPixel.Shading.Model;
using PdfPixel.Skia.Cache;
using PdfPixel.Skia.Converters;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawShading(DrawShadingCommand command)
    {
        ShadingCommandCacheEntry entry = GetOrBuildShadingEntry(command.Content);

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
    /// Returns the cached primitives built for the shading content, building and storing them when the
    /// cache holds none.
    /// </summary>
    private ShadingCommandCacheEntry GetOrBuildShadingEntry(PdfShadingContent content)
    {
        ShadingCommandCacheKey key = new(content);

        if (_executionContext.Cache.GetEntry(key) is ShadingCommandCacheEntry existing)
        {
            return existing;
        }

        SKImage? image = (content.Function != null) ? PdfImageConverter.ToSkImage(content.Function.Image) : null;
        SKShader? shader = null;
        SKShader? innerShader = null;

        if (content.Axial != null)
        {
            shader = content.Axial.ToSkiaShader();
        }
        else if (content.Radial != null)
        {
            shader = content.Radial.ToSkiaOuterShader();
            innerShader = content.Radial.ToSkiaInnerShader();
        }

        SKVertices? vertices = content.Mesh?.ToSkVertices();

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
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.FillAlpha),
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
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.FillAlpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.DrawPaint(paint);
    }
}
