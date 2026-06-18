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
    private SKMatrix _totalMatrix = SKMatrix.Identity;
    private SKPath? _clipPath;
    private int _layerDepth;

    /// <summary>
    /// Current total transformation matrix, accumulated from <see cref="OnConcatMatrix"/> notifications.
    /// </summary>
    public SKMatrix TotalMatrix => _totalMatrix;

    /// <summary>
    /// Current clip path in device space, or <see langword="null"/> when nothing has been clipped yet.
    /// </summary>
    public SKPath? ClipPath => _clipPath;

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

        _clipPath?.Dispose();
        _clipPath = frame.ClipPath;

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
    public void OnConcatMatrix(SKMatrix matrix)
    {
        _stateOps.Add(new ConcatMatrixCanvasOp(matrix));
        _totalMatrix = _totalMatrix.PreConcat(matrix);
    }

    /// <summary>
    /// Combines <paramref name="path"/>, transformed by the current total matrix, into the current clip path
    /// using <paramref name="operation"/>. Called when a clip-path command is processed.
    /// </summary>
    public void OnClipPath(SKPath path, SKClipOperation operation, bool antialias)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        _stateOps.Add(new ClipPathCanvasOp(path, operation, antialias));

        using SKPath devicePath = new(path);
        devicePath.Transform(_totalMatrix);

        if (_clipPath == null)
        {
            _clipPath = new SKPath(devicePath);
            return;
        }

        SKPathOp pathOp = (operation == SKClipOperation.Difference) ? SKPathOp.Difference : SKPathOp.Intersect;
        SKPath? combinedPath = _clipPath.Op(devicePath, pathOp);

        if (combinedPath != null)
        {
            _clipPath.Dispose();
            _clipPath = combinedPath;
        }
    }

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

    /// <inheritdoc />
    public void Dispose()
    {
        _clipPath?.Dispose();
        _clipPath = null;

        foreach (Frame frame in _frames)
        {
            frame.Dispose();
        }

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

    private void Push(bool isLayer)
    {
        SKPath? clipPathSnapshot = (_clipPath == null) ? null : new SKPath(_clipPath);
        _frames.Push(new Frame(_totalMatrix, clipPathSnapshot, isLayer));
    }

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

    private readonly struct Frame : IDisposable
    {
        public Frame(SKMatrix matrix, SKPath? clipPath, bool isLayer)
        {
            Matrix = matrix;
            ClipPath = clipPath;
            IsLayer = isLayer;
        }

        public SKMatrix Matrix { get; }

        public SKPath? ClipPath { get; }

        public bool IsLayer { get; }

        public void Dispose() => ClipPath?.Dispose();
    }
}
