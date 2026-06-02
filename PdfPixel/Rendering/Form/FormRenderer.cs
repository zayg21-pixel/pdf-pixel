using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;
using System.Collections;
using System.Collections.Generic;

namespace PdfPixel.Rendering.Form;

/// <summary>
/// Standard implementation of Form renderer for rendering PDF Form XObjects.
/// </summary>
public class FormRenderer : IFormRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public FormRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer;
        _loggerFactory = loggerFactory;
    }

    /// <inheritdoc/>
    public void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        if (processor == null)
        {
            throw new ArgumentNullException(nameof(processor));
        }

        if (formXObject == null)
        {
            throw new ArgumentNullException(nameof(formXObject));
        }

        if (graphicsState == null)
        {
            throw new ArgumentNullException(nameof(graphicsState));
        }

        uint objectNumber = formXObject.XObject.Reference.ObjectNumber;

        if (graphicsState.RecursionGuard.Contains(objectNumber))
        {
            return;
        }

        graphicsState.RecursionGuard.Add(objectNumber);

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, graphicsState);
        softMaskScope.BeginDrawContent();

        processor.Process(new SaveStateCommand());

        // Apply form matrix if present
        processor.Process(new ConcatMatrixCommand(formXObject.Matrix));

        // Clip to /BBox
        processor.Process(new ClipPathCommand(formXObject.BBox, SKClipOperation.Intersect));

        if (formXObject.TransparencyGroup != null)
        {
            SKPaint formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);
            processor.Process(new SaveLayerCommand(formXObject.BBox, formPaint));
        }

        // Decode and render content with a cloned state that clears parent soft mask
        PdfCommandRecorder recorder = new();

        System.ReadOnlyMemory<byte> content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            FormXObjectPageWrapper formPage = formXObject.GetFormPage();

            PdfGraphicsState localGs = new(formPage, graphicsState);
            localGs.CTM = formXObject.Matrix;

            PdfContentStreamRenderer renderer = new(_renderer, formPage);
            PdfParseContext parseContext = new(content);

            renderer.RenderContext(recorder, ref parseContext, localGs);
        }

        DrawRecordingCommand recording = new(recorder);
        processor.Process(recording);

        if (formXObject.TransparencyGroup != null)
        {
            processor.Process(new RestoreStateCommand());
        }

        processor.Process(new RestoreStateCommand());

        softMaskScope.EndDrawContent();


        graphicsState.RecursionGuard.Remove(objectNumber);
    }
}
