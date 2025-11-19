using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Forms;
using PdfReader.Parsing;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Utilities;
using SkiaSharp;
using System.Collections.Generic;

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

    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState, HashSet<uint> processingXObjects)
    {
        uint objectNumber = formXObject.XObject.Reference.ObjectNumber;

        if (processingXObjects.Contains(objectNumber))
        {
            // avoid circular
            return;
        }

        processingXObjects.Add(objectNumber);

        // Use form paint to composite the whole form with correct alpha/blend when needed
        using var formPaint = PdfPaintFactory.CreateLayerPaint(graphicsState);

        // Apply form matrix if present
        canvas.Concat(formXObject.Matrix);

        // Clip to /BBox
        canvas.ClipRect(formXObject.BBox, antialias: true);
        canvas.SaveLayer(formXObject.BBox, formPaint);

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, graphicsState);
        softMaskScope.BeginDrawContent();

        try
        {
            // Decode and render content with a cloned state that clears parent soft mask
            var content = formXObject.GetFormData();
            if (!content.IsEmpty)
            {
                var parseContext = new PdfParseContext(content);
                var formPage = formXObject.GetFormPage();
                var localGs = graphicsState.Clone();
                // Prevent double-application: global soft mask is applied by outer wrapper
                localGs.SoftMask = null;
                var renderer = new PdfContentStreamRenderer(_renderer, formPage);
                renderer.RenderContext(canvas, ref parseContext, localGs, processingXObjects);
            }
        }
        finally
        {
            canvas.Restore();

            softMaskScope.EndDrawContent();
            processingXObjects.Remove(objectNumber);
        }
    }
}
