using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Tracks the total transformation matrix and current clip across a command replay, mirroring
/// the canvas save/restore stack without depending on the canvas itself. Commands that change
/// canvas state (matrix, clip, layers, save/restore) notify this class so its values stay in sync.
/// </summary>
public sealed class PdfCommandExecutionFrames
{
    private readonly Stack<PdfCommandFrame> _frames = [];
    private PdfMatrix _totalMatrix = PdfMatrix.Identity;
    private PdfClipState? _currentClip;
    private int _layerDepth;

    /// <summary>
    /// Current total transformation matrix, accumulated from <see cref="OnConcatMatrix"/> notifications.
    /// </summary>
    public PdfMatrix TotalMatrix => _totalMatrix;

    /// <summary>
    /// Most recently applied clip operation, or null when no clip has been applied yet at the current save depth.
    /// </summary>
    public PdfClipState? CurrentClip => _currentClip;

    /// <summary>
    /// Currently active saved frames, innermost (most recently pushed) first.
    /// </summary>
    public IReadOnlyCollection<PdfCommandFrame> Frames => _frames;

    /// <summary>
    /// Number of currently active saved states, mirroring the depth of the canvas save stack.
    /// </summary>
    public int SavesCount => _frames.Count;

    /// <summary>
    /// Number of currently active layers started via <see cref="OnSaveLayer"/>.
    /// </summary>
    public int LayerDepth => _layerDepth;

    /// <summary>
    /// Pushes the current matrix and clip onto the stack. Called when a save-state command is processed.
    /// </summary>
    public void OnSaveState() => Push(isLayer: false);

    /// <summary>
    /// Pushes the current matrix and clip onto the stack and increments <see cref="LayerDepth"/>.
    /// Starting a layer implicitly behaves like a save, so layer commands notify this the same way
    /// as save-state commands, plus track the layer depth.
    /// </summary>
    public void OnSaveLayer()
    {
        Push(isLayer: true);
        _layerDepth++;
    }

    /// <summary>
    /// Pops the most recently saved matrix and clip, restoring them as current,
    /// and decrements <see cref="LayerDepth"/> when the popped frame was a layer.
    /// Called when a restore-state command is processed.
    /// </summary>
    public void OnRestoreState()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        PdfCommandFrame frame = _frames.Pop();

        _totalMatrix = frame.Matrix;
        _currentClip = frame.Clip;

        if (frame.IsLayer)
        {
            _layerDepth--;
        }
    }

    /// <summary>
    /// Concatenates <paramref name="matrix"/> onto the current total transformation matrix.
    /// Called when a matrix-concatenation command is processed.
    /// </summary>
    public void OnConcatMatrix(in PdfMatrix matrix) => _totalMatrix = _totalMatrix.PreConcat(matrix);

    /// <summary>
    /// Records a fill clip-path operation.
    /// </summary>
    public void OnClipPath(PdfPath path, PdfClipOperation operation) => _currentClip = PdfClipState.ForPath(path, operation);

    /// <summary>
    /// Records a stroke clip-path operation, keeping the paint whose stroke outline was clipped to.
    /// </summary>
    public void OnClipStrokePath(PdfPath path, PdfClipOperation operation, PdfPaint paint) => _currentClip = PdfClipState.ForStrokePath(path, operation, paint);

    /// <summary>
    /// Records a clip-rectangle operation as an equivalent fill clip path.
    /// </summary>
    public void OnClipRect(in PdfRectangle rect, PdfClipOperation operation)
    {
        PdfPathBuilder pathBuilder = new();
        pathBuilder.AddRect(rect);
        OnClipPath(pathBuilder.ToPath(), operation);
    }

    /// <summary>
    /// Resets all tracked state back to initial values so the frames can be reused
    /// for a second pass over the same command sequence.
    /// </summary>
    public void Reset()
    {
        _frames.Clear();
        _totalMatrix = PdfMatrix.Identity;
        _currentClip = null;
        _layerDepth = 0;
    }

    private void Push(bool isLayer) => _frames.Push(new PdfCommandFrame(_totalMatrix, _currentClip, isLayer));
}
