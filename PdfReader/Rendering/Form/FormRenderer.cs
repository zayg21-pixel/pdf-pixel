using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Forms;
using PdfReader.Parsing;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Utilities;
using SkiaSharp;

namespace PdfReader.Rendering.Form;

/// <summary>
/// Standard implementation of Form renderer for rendering PDF Form XObjects.
/// </summary>
public class FormRenderer : IFormRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _loggerFactory;

    public FormRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer;
        _loggerFactory = loggerFactory;
    }

    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        uint objectNumber = formXObject.XObject.Reference.ObjectNumber;

        if (graphicsState.RecursionGuard.Contains(objectNumber))
        {
            return;
        }

        graphicsState.RecursionGuard.Add(objectNumber);

        int count = canvas.Save();

        // Apply form matrix if present
        canvas.Concat(formXObject.Matrix);

        // Clip to /BBox
        canvas.ClipRect(formXObject.BBox, antialias: true);

        var localGs = new PdfGraphicsState(graphicsState.Page, graphicsState.RecursionGuard, graphicsState.RenderingParameters);
        localGs.CTM = formXObject.Matrix;

        if (formXObject.TransparencyGroup != null)
        {
            using var formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);
            canvas.SaveLayer(formXObject.BBox, formPaint);
        }

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, graphicsState);
        softMaskScope.BeginDrawContent();

        // Decode and render content with a cloned state that clears parent soft mask
        var content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            var parseContext = new PdfParseContext(content);
            var formPage = formXObject.GetFormPage();

            var renderer = new PdfContentStreamRenderer(_renderer, formPage);
            renderer.RenderContext(canvas, ref parseContext, localGs);
        }

        softMaskScope.EndDrawContent();

        canvas.RestoreToCount(count);

        graphicsState.RecursionGuard.Remove(objectNumber);
    }
}
