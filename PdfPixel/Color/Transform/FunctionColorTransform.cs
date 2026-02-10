using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Represents a function that processes a color vector and returns a transformed color vector.
/// </summary>
/// <param name="input">The input color vector.</param>
/// <returns>The transformed color vector.</returns>
internal delegate Vector4 PixelProcessorFunction(Vector4 input);

/// <summary>
/// Implements <see cref="IColorTransform"/> using a user-supplied pixel processing function.
/// </summary>
internal sealed class FunctionColorTransform : IColorTransform
{
    private readonly PixelProcessorFunction _function;

    /// <summary>
    /// Initializes a new instance of the <see cref="FunctionColorTransform"/> class with the specified pixel processor function.
    /// </summary>
    /// <param name="function">A delegate that processes and transforms a color vector.</param>
    public FunctionColorTransform(PixelProcessorFunction function)
    {
        _function = function;
    }

    public bool IsIdentity => false;

    /// <summary>
    /// Transforms the input color vector using the provided pixel processor function.
    /// </summary>
    /// <param name="color">The input color vector.</param>
    /// <returns>The transformed color vector.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        return _function(color);
    }
}
