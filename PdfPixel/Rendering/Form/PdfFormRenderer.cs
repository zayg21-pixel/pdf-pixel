using Microsoft.Extensions.Logging;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Parsing;
using PdfPixel.Tagging;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Rendering.Form;

/// <summary>
/// Standard implementation of Form renderer for rendering PDF Form XObjects.
/// </summary>
internal class PdfFormRenderer
{
    private readonly PdfRenderer _renderer;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Initializes the renderer with the PDF renderer pipeline and logger factory.
    /// </summary>
    public PdfFormRenderer(PdfRenderer renderer, ILoggerFactory loggerFactory)
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
            processor.Process(new PdfContentCommand<PdfForm>(PdfContentKind.Form, formXObject.Matrix, formXObject.BoundingBox, formXObject));
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
        processor.Process(SaveStateCommand.Instance);

        // Apply form matrix if present
        processor.Process(new ConcatMatrixCommand(formXObject.Matrix));

        // Clip to /BBox
        PdfPathBuilder clipBuilder = new();
        clipBuilder.AddRect(formXObject.BoundingBox);
        processor.Process(new DrawPathCommand(clipBuilder.ToPath(), new PdfPaint(PdfPaintStyle.Clip)));

        PdfTransparencyGroup? group = (graphicsState.RenderingParameters.RenderContent) ? formXObject.TransparencyGroup : null;

        if (group != null)
        {
            DrawGroup(processor, formXObject, graphicsState, group);
        }
        else
        {
            processor.Process(new DrawRecordingCommand(RecordContent(formXObject, graphicsState, startsGroup: false)));
        }

        processor.Process(RestoreStateCommand.Instance);
    }

    private void DrawGroup(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState, PdfTransparencyGroup group)
    {
        PdfTransparencyGroupContext groupContext = new(group.ResolveColorSpace(graphicsState.Host.Resources), group.Isolated, group.Knockout);
        DrawRecordingCommand recording = new(RecordContent(formXObject, graphicsState, startsGroup: true));
        PdfGroupSource source = new(recording, groupContext, formXObject.BoundingBox, null);
        PdfPaint formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);

        processor.Process(new DrawGroupCommand(source, formPaint));
    }

    private PdfCommandRecorder RecordContent(PdfForm formXObject, PdfGraphicsState graphicsState, bool startsGroup)
    {
        PdfCommandRecorder recorder = new();

        ReadOnlyMemory<byte> content = formXObject.Stream.DecodeAsMemory();
        if (!content.IsEmpty)
        {
            PdfContentHost formHost = formXObject.GetFormHost(graphicsState);

            PdfGraphicsState localGs = graphicsState.CloneForHost(formHost);
            localGs.CTM = formXObject.Matrix;
            localGs.ClipBounds = localGs.CTM.MapRect(formXObject.BoundingBox);

            if (startsGroup)
            {
                localGs.FillPaint = localGs.FillPaint.WithAlpha(1f).WithBlendMode(PdfBlendMode.Normal);
                localGs.StrokePaint = localGs.StrokePaint.WithAlpha(1f).WithBlendMode(PdfBlendMode.Normal);
                localGs.SoftMask = null;
            }

            PdfContentStreamRenderer renderer = new(_renderer, formHost);
            PdfParseContext parseContext = new(content, formHost.Document);

            renderer.RenderContext(recorder, ref parseContext, localGs);
        }

        return recorder;
    }
}
