using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using System;

namespace PdfPixel.Transparency;

/// <summary>
/// Disposable scope to render content with an optional soft mask and transparency group applied.
/// </summary>
internal sealed class SoftMaskDrawingScope : IDisposable
{
    private readonly IPdfRenderer _renderer;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfSoftMask _softMask;
    private readonly PdfGraphicsState _graphicsState;
    private readonly PdfRectangle _layerBounds;
    private readonly PdfMatrix _maskMatrix;
    private readonly PdfMatrix _worldToMaskForm;

    private bool _disposed;

    private SoftMaskDrawingScope(
        IPdfRenderer renderer,
        IPdfCommandProcessor processor,
        PdfSoftMask softMask,
        PdfGraphicsState graphicsState,
        in PdfRectangle layerBounds,
        in PdfMatrix maskMatrix,
        in PdfMatrix worldToMaskForm)
    {
        _renderer = renderer;
        _processor = processor;
        _softMask = softMask;
        _graphicsState = graphicsState;
        _layerBounds = layerBounds;
        _maskMatrix = maskMatrix;
        _worldToMaskForm = worldToMaskForm;
    }

    /// <summary>
    /// Begins a drawing scope that applies the soft mask of the graphics state to the content drawn until
    /// the scope is disposed.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="graphicsState">Current graphics state (provides the soft mask).</param>
    /// <param name="contentBounds">
    /// Area the content about to be drawn can reach, in current user space, which the mask layers are
    /// confined to. Content with no extent of its own passes <see cref="PdfGraphicsState.GetUserSpaceClipBounds"/>.
    /// </param>
    /// <returns>The scope, or <see langword="null"/> when no soft mask applies to the content.</returns>
    public static SoftMaskDrawingScope? Begin(
        IPdfRenderer renderer,
        IPdfCommandProcessor processor,
        PdfGraphicsState graphicsState,
        in PdfRectangle contentBounds)
    {
        if (graphicsState == null)
        {
            throw new ArgumentNullException(nameof(graphicsState));
        }

        PdfSoftMask? softMask = graphicsState.SoftMask;

        if (processor == null || softMask == null || !graphicsState.RenderingParameters.RenderContent)
        {
            return null;
        }

        PdfMatrix inverseCtm = graphicsState.CTM.Invert();
        PdfMatrix worldToMaskForm = PdfMatrix.Concat(graphicsState.SoftMaskCTM, softMask.MaskForm.Matrix);
        PdfMatrix maskMatrix = PdfMatrix.Concat(inverseCtm, worldToMaskForm);
        PdfRectangle layerBounds = PdfRectangle.Intersect(inverseCtm.MapRect(graphicsState.ClipBounds), contentBounds);

        if (layerBounds.IsEmpty)
        {
            return null;
        }

        processor.Process(new SaveLayerCommand(layerBounds));

        return new SoftMaskDrawingScope(renderer, processor, softMask, graphicsState, layerBounds, maskMatrix, worldToMaskForm);
    }

    /// <summary>
    /// Composites the soft mask onto the content drawn since <see cref="Begin"/> and restores the layers
    /// the scope opened.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        PdfCommandRecorder recorder = new();
        recorder.Process(SaveStateCommand.Instance);

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            PdfColor backgroundColor = _softMask.GetBackgroundColor(_graphicsState);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(_layerBounds);

            recorder.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        recorder.Process(new ConcatMatrixCommand(_maskMatrix));
        recorder.Process(new ClipRectangleCommand(_softMask.MaskForm.BoundingBox, PdfClipOperation.Intersect));

        PdfCommandRecorder? maskFormRecording = SoftMaskUtilities.GetMaskFormRecording(
            _renderer,
            _softMask,
            _softMask.MaskForm,
            _graphicsState,
            _worldToMaskForm);

        if (maskFormRecording != null)
        {
            recorder.Process(new DrawRecordingCommand(maskFormRecording));
        }

        recorder.Process(RestoreStateCommand.Instance);

        PdfPaint maskPaint = PdfPaintFactory.CreateSoftMaskPaint(_softMask.Subtype, _softMask.TransferFunction);

        _processor.Process(new SaveLayerCommand(_layerBounds, maskPaint));

        _processor.Process(new DrawRecordingCommand(recorder));
        _processor.Process(RestoreLayerCommand.Instance);

        _processor.Process(RestoreLayerCommand.Instance);
    }
}
