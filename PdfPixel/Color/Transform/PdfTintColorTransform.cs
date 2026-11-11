using PdfPixel.Functions;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Maps tints through a tint transform function and converts the result by the transform of the
/// alternate color space. Without a function, the tints reach the alternate transform unchanged.
/// </summary>
public sealed class PdfTintColorTransform : IPdfColorTransform
{
    private readonly PdfFunction? _tintTransform;
    private readonly IPdfColorTransform _alternateTransform;
    private readonly float[] _tints;
    private readonly float[] _alternateColor;

    /// <summary>
    /// Initializes a transform mapping <paramref name="componentCount"/> tints.
    /// </summary>
    /// <param name="componentCount">Number of tints of each color.</param>
    /// <param name="tintTransform">Function mapping the tints to the alternate color space, or null when absent.</param>
    /// <param name="alternateTransform">Transform of the alternate color space.</param>
    public PdfTintColorTransform(int componentCount, PdfFunction? tintTransform, IPdfColorTransform alternateTransform)
    {
        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        InputComponentCount = componentCount;
        _tintTransform = tintTransform;
        _alternateTransform = alternateTransform ?? throw new ArgumentNullException(nameof(alternateTransform));
        _tints = new float[componentCount];
        _alternateColor = new float[Math.Max(1, alternateTransform.InputComponentCount)];
    }

    /// <inheritdoc />
    public int InputComponentCount { get; }

    /// <inheritdoc />
    public int OutputComponentCount => _alternateTransform.OutputComponentCount;

    /// <inheritdoc />
    public void Transform(ref float input, ref float output)
    {
        if (_tintTransform == null)
        {
            _alternateTransform.Transform(ref input, ref output);
        }
        else
        {
            // TODO: evaluate the function from and into references once PdfFunction takes them; the tints and its
            // result are copied for now.
            Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref _tints[0]), ref Unsafe.As<float, byte>(ref input), (uint)(_tints.Length * sizeof(float)));
            ReadOnlySpan<float> alternateColor = _tintTransform.Evaluate(_tints);

            Array.Clear(_alternateColor, 0, _alternateColor.Length);
            alternateColor.Slice(0, Math.Min(alternateColor.Length, _alternateColor.Length)).CopyTo(_alternateColor);
            _alternateTransform.Transform(ref _alternateColor[0], ref output);
        }
    }
}
