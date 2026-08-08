using PdfPixel.Commands.Cache;
using PdfPixel.Commands.Converters;
using PdfPixel.Shading.Model;
using SkiaSharp;

namespace PdfPixel.Commands.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawShading(DrawShadingCommand command)
    {
        ShadingCommandCacheEntry entry = CommandHelpers.GetOrBuildShadingEntry(command, _executionContext);

        switch (command.Context.Shading.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                ExecuteShadingFunctionBased(entry);
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

    private void ExecuteShadingFunctionBased(ShadingCommandCacheEntry entry)
    {
        if (entry.Function == null)
        {
            return;
        }

        using SKImage image = PdfImageConverter.ToSkImage(entry.Function.Image);

        _canvas.Save();
        _canvas.Concat(entry.Function.Matrix.ToSkMatrix());
        _canvas.DrawImage(image, SKPoint.Empty, SKSamplingOptions.Default);
        _canvas.Restore();
    }

    private void ExecuteShadingAxial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Axial == null)
        {
            return;
        }

        using SKPaint paint = entry.Axial.ToSkiaPaint();
        DrawShadingPaintToCanvas(command, paint);
    }

    private void ExecuteShadingRadial(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Radial == null)
        {
            return;
        }

        using SKPaint innerPaint = entry.Radial.ToSkiaInnerPaint();
        DrawShadingPaintToCanvas(command, innerPaint);

        using SKPaint outerPaint = entry.Radial.ToSkiaOuterPaint();
        DrawShadingPaintToCanvas(command, outerPaint);
    }

    private void ExecuteShadingMesh(DrawShadingCommand command, ShadingCommandCacheEntry entry)
    {
        if (entry.Mesh == null)
        {
            return;
        }

        DrawShadingVerticesToCanvas(command, entry.Mesh);
    }

    private void DrawShadingPaintToCanvas(DrawShadingCommand command, SKPaint paint)
    {
        paint.Color = SkiaCommandUtilities.ApplyAlpha(paint.Color, command.Context.FillAlpha);
        paint.IsAntialias = _executionContext.Parameters.Antialias;

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);

        _canvas.DrawPaint(paint);
    }

    private void DrawShadingVerticesToCanvas(DrawShadingCommand command, PdfVertices vertices)
    {
        using SKPaint paint = CreateShadingVerticesPaint(command);

        using SKVertices skVertices = PdfShadingConverter.ToSkVertices(vertices);
        _canvas.DrawVertices(skVertices, SKBlendMode.DstIn, paint);
    }

    private SKPaint CreateShadingVerticesPaint(DrawShadingCommand command)
    {
        SKPaint paint = new()
        {
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.Context.FillAlpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        return paint;
    }
}
