using PdfPixel.Snap.Painting;
using System.Runtime.CompilerServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// A run of an element's pixels composited into a <see cref="SnapLayer"/> (ISO 32000-2 11.4.8), its alpha and shape
/// kept as factors: αs = coverage × alpha × mask × constant alpha, fs = coverage × shape, times mask × constant alpha
/// when alpha is shape (11.6.4).
/// </summary>
internal readonly ref struct SnapSourceRow
{
    public SnapSourceRow(
        int y,
        int left,
        int count,
        in SnapRowValues color,
        in SnapRowValues coverage,
        in SnapRowValues shape,
        in SnapRowValues alpha,
        in SnapRowValues mask,
        float constantAlpha,
        bool alphaIsShape)
    {
        Y = y;
        Left = left;
        Count = count;
        Color = color;
        Coverage = coverage;
        Shape = shape;
        Alpha = alpha;
        Mask = mask;
        ConstantAlpha = constantAlpha;
        AlphaIsShape = alphaIsShape;
    }

    public int Y { get; }

    public int Left { get; }

    public int Count { get; }

    /// <summary>
    /// Gets Cs in the layer's blending space, not premultiplied.
    /// </summary>
    public SnapRowValues Color { get; }

    /// <summary>
    /// Gets how much of each pixel the element covers, clip included.
    /// </summary>
    public SnapRowValues Coverage { get; }

    /// <summary>
    /// Gets the element's own shape, coverage excluded.
    /// </summary>
    public SnapRowValues Shape { get; }

    /// <summary>
    /// Gets the element's own alpha, coverage, mask and constant alpha excluded.
    /// </summary>
    public SnapRowValues Alpha { get; }

    /// <summary>
    /// Gets the soft mask or an image's own mask.
    /// </summary>
    public SnapRowValues Mask { get; }

    public float ConstantAlpha { get; }

    /// <summary>
    /// Gets whether the mask and the constant alpha are shape (AIS).
    /// </summary>
    public bool AlphaIsShape { get; }

    /// <summary>
    /// Returns αs at a pixel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSourceAlpha(float coverage, float alpha, float mask) => coverage * alpha * mask * ConstantAlpha;

    /// <summary>
    /// Returns fs at a pixel.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float GetSourceShape(float coverage, float shape, float mask)
    {
        float sourceShape = coverage * shape;

        if (AlphaIsShape)
        {
            sourceShape *= mask * ConstantAlpha;
        }

        return sourceShape;
    }
}
