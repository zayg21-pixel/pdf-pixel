using PdfPixel.Commands;
using PdfPixel.Shading.Model;
using PdfPixel.Skia.Converters;
using SkiaSharp;

namespace PdfPixel.Skia;

public sealed partial class SkCanvasCommandProcessor
{
    private void ExecuteDrawShading(DrawShadingCommand command)
    {
        switch (command.Content.ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                ExecuteShadingFunctionBased(command);
                break;
            }
            case PdfShadingType.Axial:
            {
                ExecuteShadingAxial(command);
                break;
            }
            case PdfShadingType.Radial:
            {
                ExecuteShadingRadial(command);
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                ExecuteShadingMesh(command);
                break;
            }
        }
    }

    private void ExecuteShadingFunctionBased(DrawShadingCommand command)
    {
        if (command.Content.Function == null)
        {
            return;
        }

        using SKImage image = PdfImageConverter.ToSkImage(command.Content.Function.Image);

        _canvas.Save();
        _canvas.Concat(command.Content.Function.Matrix.ToSkMatrix());
        _canvas.DrawImage(image, SKPoint.Empty, SKSamplingOptions.Default);
        _canvas.Restore();
    }

    private void ExecuteShadingAxial(DrawShadingCommand command)
    {
        if (command.Content.Axial == null)
        {
            return;
        }

        using SKPaint paint = command.Content.Axial.ToSkiaPaint();
        DrawShadingPaintToCanvas(command, paint);
    }

    private void ExecuteShadingRadial(DrawShadingCommand command)
    {
        if (command.Content.Radial == null)
        {
            return;
        }

        using SKPaint innerPaint = command.Content.Radial.ToSkiaInnerPaint();
        DrawShadingPaintToCanvas(command, innerPaint);

        using SKPaint outerPaint = command.Content.Radial.ToSkiaOuterPaint();
        DrawShadingPaintToCanvas(command, outerPaint);
    }

    private void ExecuteShadingMesh(DrawShadingCommand command)
    {
        if (command.Content.Mesh == null)
        {
            return;
        }

        DrawShadingVerticesToCanvas(command, command.Content.Mesh);
    }

    private void DrawShadingPaintToCanvas(DrawShadingCommand command, SKPaint paint)
    {
        paint.Color = SkiaCommandUtilities.ApplyAlpha(paint.Color, command.FillAlpha);
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
            Color = SkiaCommandUtilities.ApplyAlpha(SKColors.Black, command.FillAlpha),
            IsAntialias = _executionContext.Parameters.Antialias
        };

        SkiaCommandUtilities.ModifyPaint(paint, _executionContext);
        return paint;
    }
}
