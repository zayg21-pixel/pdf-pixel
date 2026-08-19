using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using System;

namespace PdfPixel.Transparency.Utilities;

/// <summary>
/// Disposable scope to render content with an optional soft mask and transparency group applied.
/// </summary>
public sealed class SoftMaskDrawingScope : IDisposable
{
    private readonly IPdfRenderer _renderer;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfSoftMask? _softMask;
    private readonly PdfGraphicsState _graphicsState;
    private readonly PdfRectangle _contentBounds;

    private PdfRectangle _layerBounds;
    private PdfMatrix _maskMatrix;
    private PdfMatrix _worldToMaskForm;
    private bool _began;
    private bool _shouldApplyMask;
    private bool _disposed;

    /// <summary>
    /// Create a new soft mask drawing scope.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="graphicsState">Current graphics state (provides the soft mask).</param>
    /// <param name="contentBounds">
    /// Area the content about to be drawn can reach, in current user space, which the mask layers are
    /// confined to. Content with no extent of its own passes <see cref="PdfGraphicsState.GetUserSpaceClipBounds"/>.
    /// </param>
    public SoftMaskDrawingScope(
        IPdfRenderer renderer,
        IPdfCommandProcessor processor,
        PdfGraphicsState graphicsState,
        in PdfRectangle contentBounds)
    {
        if (graphicsState == null)
        {
            throw new ArgumentNullException(nameof(graphicsState));
        }

        _renderer = renderer;
        _processor = processor;
        _softMask = graphicsState.SoftMask;
        _graphicsState = graphicsState;
        _contentBounds = contentBounds;
    }

    /// <summary>
    /// Begins the drawing scope. If a valid soft mask is provided, creates a layer to capture content.
    /// </summary>
    public void BeginDrawContent()
    {
        if (_began)
        {
            return;
        }

        _began = true;

        if (_processor == null)
        {
            return;
        }

        _shouldApplyMask = true;

        if (_softMask == null || _softMask.MaskForm == null)
        {
            _shouldApplyMask = false;
            return;
        }

        PdfMatrix inverseCtm = _graphicsState.CTM.Invert();

        _worldToMaskForm = PdfMatrix.Concat(_graphicsState.SoftMaskCTM, _softMask.MaskForm.Matrix);
        _maskMatrix = PdfMatrix.Concat(inverseCtm, _worldToMaskForm);
        _layerBounds = PdfRectangle.Intersect(inverseCtm.MapRect(_graphicsState.ClipBounds), _contentBounds);

        if (_layerBounds.IsEmpty)
        {
            _shouldApplyMask = false;
            return;
        }

        _processor.Process(new SaveLayerCommand(_layerBounds));
    }

    /// <summary>
    /// Ends the drawing scope. When a soft mask is active, composites it onto the content drawn since
    /// <see cref="BeginDrawContent"/> and restores the layers it opened.
    /// </summary>
    public void EndDrawContent()
    {
        if (!_began)
        {
            return;
        }

        if (!_shouldApplyMask || _softMask?.MaskForm == null)
        {
            return;
        }

        PdfCommandRecorder recorder = new();
        recorder.Process(SaveStateCommand.Instance);

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            PdfColor backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent, _graphicsState.TransferFunction);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(_layerBounds);

            recorder.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        recorder.Process(new ConcatMatrixCommand(_maskMatrix));
        recorder.Process(new ClipRectangleCommand(_softMask.MaskForm.BBox, PdfClipOperation.Intersect));

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
        _shouldApplyMask = false;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Ensure proper teardown if the caller forgot to call EndDrawContent.
        if (_began && _shouldApplyMask)
        {
            EndDrawContent();
        }
    }
}
