using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Tracks the total transformation matrix and clip path across a command replay,
/// mirroring <see cref="SKCanvas.Save"/>/<see cref="SKCanvas.Restore"/> semantics
/// without depending on the canvas itself. Commands that change canvas state
/// (matrix, clip, layers, save/restore) notify this class so its values stay in sync.
/// </summary>
public sealed class PdfCommandExecutionFrames : IDisposable
{
    private readonly Stack<Frame> _frames = [];
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
    public void OnSaveState() => Push(isLayer: false);

    /// <summary>
    /// Pushes the current matrix and clip path onto the stack and increments <see cref="LayerDepth"/>.
    /// <see cref="SKCanvas.SaveLayer(SKRect, SKPaint)"/> implicitly behaves like a save, so layer
    /// commands notify this the same way as save-state commands, plus track the layer depth.
    /// </summary>
    public void OnSaveLayer()
    {
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
    }

    /// <summary>
    /// Concatenates <paramref name="matrix"/> onto the current total transformation matrix.
    /// Called when a matrix-concatenation command is processed.
    /// </summary>
    public void OnConcatMatrix(SKMatrix matrix) => _totalMatrix = _totalMatrix.PostConcat(matrix);

    /// <summary>
    /// Combines <paramref name="path"/>, transformed by the current total matrix, into the current clip path
    /// using <paramref name="operation"/>. Called when a clip-path command is processed.
    /// </summary>
    public void OnClipPath(SKPath path, SKClipOperation operation)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

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

    /// <inheritdoc />
    public void Dispose()
    {
        _clipPath?.Dispose();
        _clipPath = null;

        foreach (Frame frame in _frames)
        {
            frame.ClipPath?.Dispose();
        }

        _frames.Clear();
    }

    private void Push(bool isLayer) => _frames.Push(new Frame(_totalMatrix, (_clipPath == null) ? null : new SKPath(_clipPath), isLayer));

    private readonly struct Frame
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
    }
}
