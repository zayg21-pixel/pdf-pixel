using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering.State;
using PdfPixel.Tagging.Model;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using System;

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

        uint objectNumber = formXObject.Reference.ObjectNumber;

        if (graphicsState.RecursionGuard.Contains(objectNumber))
        {
            return;
        }

        graphicsState.RecursionGuard.Add(objectNumber);

        PdfStructureTree? structureTree = null;
        if (graphicsState.RenderingParameters.ExtractContent)
        {
            structureTree = graphicsState.Host.Document.StructureTree;
        }

        PdfContentStreamScope contentStreamScope = new(PdfContentStreamType.Form);
        PdfXObjectScope? xObjectScope = PdfXObjectScope.FromXObject(formXObject, structureTree);

        if (xObjectScope != null)
        {
            processor.Process(new BeginScopeCommand(xObjectScope));
        }

        if (graphicsState.RenderingParameters.ExtractContent)
        {
            processor.Process(new PdfContentCommand<PdfForm>(PdfContentKind.Form, formXObject.Matrix, formXObject.BBox, formXObject));
        }

        processor.Process(new BeginScopeCommand(contentStreamScope));

        DrawFormContent(processor, formXObject, graphicsState);

        processor.Process(new EndScopeCommand());

        if (xObjectScope != null)
        {
            processor.Process(new EndScopeCommand());
        }

        graphicsState.RecursionGuard.Remove(objectNumber);
    }

    private void DrawFormContent(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        PdfRectangle contentBounds = formXObject.Matrix.MapRect(formXObject.BBox);

        using SoftMaskDrawingScope softMaskScope = new(_renderer, processor, graphicsState, contentBounds);
        softMaskScope.BeginDrawContent();

        processor.Process(SaveStateCommand.Instance);

        // Apply form matrix if present
        processor.Process(new ConcatMatrixCommand(formXObject.Matrix));

        // Clip to /BBox
        processor.Process(new ClipRectangleCommand(formXObject.BBox, PdfClipOperation.Intersect));

        bool needsGroupLayer = graphicsState.RenderingParameters.RenderContent
            && formXObject.TransparencyGroup != null
            && (formXObject.TransparencyGroup.RequiresLayer
                || graphicsState.FillPaint.RequiresComposition);

        if (needsGroupLayer)
        {
            PdfPaint formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);
            processor.Process(new SaveLayerCommand(formXObject.BBox, formPaint));
        }

        // Decode and render content with a cloned state that clears parent soft mask
        PdfCommandRecorder recorder = new();

        ReadOnlyMemory<byte> content = formXObject.XObject.Stream.DecodeAsMemory();
        if (!content.IsEmpty)
        {
            PdfContentHost formHost = formXObject.GetFormHost(graphicsState);

            PdfGraphicsState localGs = graphicsState.CloneForHost(formHost);
            localGs.SoftMask = null;
            localGs.TextClipPath = null;
            localGs.CTM = formXObject.Matrix;
            localGs.ClipBounds = localGs.CTM.MapRect(formXObject.BBox);

            if (needsGroupLayer)
            {
                localGs.FillPaint = localGs.FillPaint.WithAlpha(1f).WithBlendMode(PdfBlendMode.Normal);
                localGs.StrokePaint = localGs.StrokePaint.WithAlpha(1f).WithBlendMode(PdfBlendMode.Normal);
            }

            PdfContentStreamRenderer renderer = new(_renderer, formHost);
            PdfParseContext parseContext = new(content);

            renderer.RenderContext(recorder, ref parseContext, localGs);
        }

        DrawRecordingCommand recording = new(recorder);
        processor.Process(recording);

        if (needsGroupLayer)
        {
            processor.Process(RestoreLayerCommand.Instance);
        }

        processor.Process(RestoreStateCommand.Instance);

        softMaskScope.EndDrawContent();
    }
}
