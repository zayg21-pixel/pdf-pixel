using PdfPixel.Geometry;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Tracks the total transformation matrix and clip path across a command replay,
/// mirroring <see cref="SKCanvas.Save"/>/<see cref="SKCanvas.Restore"/> semantics
/// without depending on the canvas itself. Commands that change canvas state
/// (matrix, clip, layers, save/restore) notify this class so its values stay in sync.
/// Also records a replayable sequence of canvas-state operations so a fresh
/// <see cref="SKCanvas"/> can be brought to the same state via <see cref="ApplyStateTo"/>.
/// </summary>
public sealed class PdfCommandExecutionFrames : IDisposable
{
    private readonly Stack<Frame> _frames = [];
    private readonly List<CanvasStateOp> _stateOps = [];
    private readonly Stack<int> _savePoints = [];
    private PdfMatrix _totalMatrix = PdfMatrix.Identity;
    private int _layerDepth;

    /// <summary>
    /// Current total transformation matrix, accumulated from <see cref="OnConcatMatrix"/> notifications.
    /// </summary>
    public PdfMatrix TotalMatrix => _totalMatrix;

    /// <summary>
    /// Number of currently active saved states, mirroring the depth of the canvas save stack.
    /// </summary>
    public int SavesCount => _frames.Count;

    /// <summary>
    /// Number of currently active layers started via <see cref="OnSaveLayer"/>.
    /// </summary>
    public int LayerDepth => _layerDepth;

    /// <summary>
    /// Pushes the current matrix and clip path onto the stack. Called when a save-state command is processed.
    /// </summary>
    public void OnSaveState()
    {
        _savePoints.Push(_stateOps.Count);
        _stateOps.Add(new SaveCanvasOp());
        Push(isLayer: false);
    }

    /// <summary>
    /// Pushes the current matrix and clip path onto the stack and increments <see cref="LayerDepth"/>.
    /// <see cref="SKCanvas.SaveLayer(SKRect, SKPaint)"/> implicitly behaves like a save, so layer
    /// commands notify this the same way as save-state commands, plus track the layer depth.
    /// SaveLayer is NOT added to the replayable state sequence because flushing only occurs
    /// at <see cref="LayerDepth"/> == 0, guaranteeing all layers are balanced before <see cref="ApplyStateTo"/> is called.
    /// </summary>
    public void OnSaveLayer()
    {
        _savePoints.Push(_stateOps.Count);
        Push(isLayer: true);
        _layerDepth++;
    }

    /// <summary>
    /// Pops the most recently saved matrix and clip path, restoring them as current,
    /// and decrements <see cref="LayerDepth"/> when the popped frame was a layer.
    /// Called when a restore-state command is processed.
    /// </summary>
    public void OnRestoreState()
    {
        if (_frames.Count == 0)
        {
            return;
        }

        Frame frame = _frames.Pop();

        _totalMatrix = frame.Matrix;

        if (frame.IsLayer)
        {
            _layerDepth--;
        }

        if (_savePoints.Count > 0)
        {
            int savePoint = _savePoints.Pop();
            DisposeStateOpsFrom(savePoint);
            _stateOps.RemoveRange(savePoint, _stateOps.Count - savePoint);
        }
    }

    /// <summary>
    /// Concatenates <paramref name="matrix"/> onto the current total transformation matrix.
    /// Called when a matrix-concatenation command is processed.
    /// </summary>
    public void OnConcatMatrix(in PdfMatrix matrix)
    {
        _stateOps.Add(new ConcatMatrixCanvasOp(matrix.ToSkMatrix()));
        _totalMatrix = _totalMatrix.PreConcat(matrix);
    }

    /// <summary>
    /// Records a clip-path operation for replay via <see cref="ApplyStateTo"/>.
    /// </summary>
    public void OnClipPath(SKPath path, SKClipOperation operation, bool antialias)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        _stateOps.Add(new ClipPathCanvasOp(path, operation, antialias));
    }

    /// <summary>
    /// Records a clip-rectangle operation for replay via <see cref="ApplyStateTo"/>.
    /// </summary>
    public void OnClipRect(SKRect rect, SKClipOperation operation, bool antialias)
        => _stateOps.Add(new ClipRectCanvasOp(rect, operation, antialias));

    /// <summary>
    /// Replays the accumulated save/matrix/clip operations onto <paramref name="canvas"/>,
    /// bringing it to the same state as the canvas that produced this frames instance.
    /// Must only be called when <see cref="LayerDepth"/> is 0.
    /// </summary>
    public void ApplyStateTo(SKCanvas canvas)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        foreach (CanvasStateOp stateOp in _stateOps)
        {
            stateOp.Apply(canvas);
        }
    }

    /// <summary>
    /// Resets all tracked state back to initial values so the frames can be reused
    /// for a second pass over the same command sequence.
    /// </summary>
    public void Reset()
    {
        _frames.Clear();

        DisposeStateOpsFrom(0);
        _stateOps.Clear();
        _savePoints.Clear();

        _totalMatrix = PdfMatrix.Identity;
        _layerDepth = 0;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        foreach (CanvasStateOp state in _stateOps)
        {
            state.Dispose();
        }

        _frames.Clear();

        DisposeStateOpsFrom(0);
        _stateOps.Clear();
    }

    private void DisposeStateOpsFrom(int startIndex)
    {
        for (int i = startIndex; i < _stateOps.Count; i++)
        {
            _stateOps[i].Dispose();
        }
    }

    private void Push(bool isLayer) => _frames.Push(new Frame(_totalMatrix, isLayer));

    private abstract class CanvasStateOp : IDisposable
    {
        public abstract void Apply(SKCanvas canvas);

        public virtual void Dispose()
        {
        }
    }

    private sealed class SaveCanvasOp : CanvasStateOp
    {
        public override void Apply(SKCanvas canvas) => canvas.Save();
    }

    private sealed class ConcatMatrixCanvasOp : CanvasStateOp
    {
        private readonly SKMatrix _matrix;

        public ConcatMatrixCanvasOp(SKMatrix matrix) => _matrix = matrix;

        public override void Apply(SKCanvas canvas) => canvas.Concat(_matrix);
    }

    private sealed class ClipPathCanvasOp : CanvasStateOp
    {
        private readonly SKPath _path;
        private readonly SKClipOperation _operation;
        private readonly bool _antialias;

        public ClipPathCanvasOp(SKPath path, SKClipOperation operation, bool antialias)
        {
            _path = new SKPath(path);
            _operation = operation;
            _antialias = antialias;
        }

        public override void Apply(SKCanvas canvas) => canvas.ClipPath(_path, _operation, _antialias);

        public override void Dispose()
        {
            base.Dispose();
            _path.Dispose();
        }
    }

    private sealed class ClipRectCanvasOp : CanvasStateOp
    {
        private readonly SKRect _rect;
        private readonly SKClipOperation _operation;
        private readonly bool _antialias;

        public ClipRectCanvasOp(SKRect rect, SKClipOperation operation, bool antialias)
        {
            _rect = rect;
            _operation = operation;
            _antialias = antialias;
        }

        public override void Apply(SKCanvas canvas) => canvas.ClipRect(_rect, _operation, _antialias);
    }

    private readonly struct Frame
    {
        public Frame(in PdfMatrix matrix, bool isLayer)
        {
            Matrix = matrix;
            IsLayer = isLayer;
        }

        public PdfMatrix Matrix { get; }

        public bool IsLayer { get; }
    }
}
