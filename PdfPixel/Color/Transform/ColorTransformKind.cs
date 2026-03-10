namespace PdfPixel.Color.Transform;

/// <summary>
/// Identifies the concrete type of an <see cref="IColorTransform"/> implementation.
/// Used for type-discriminated dispatch in hot loops to enable JIT devirtualization
/// and inlining of sealed transform types, avoiding interface vtable overhead.
/// </summary>
public enum ColorTransformKind : byte
{
    /// <summary>
    /// Per-channel TRC (tone reproduction curve) table lookup.
    /// </summary>
    PerChannelTrc = 0,

    /// <summary>
    /// Matrix-based color space transformation.
    /// </summary>
    Matrix = 1,

    /// <summary>
    /// Multi-dimensional CLUT interpolation.
    /// </summary>
    Clut = 2,

    /// <summary>
    /// Delegate-based pixel processing function.
    /// </summary>
    Function = 3,

    /// <summary>
    /// PDF transfer function transform.
    /// </summary>
    TransferFunction = 4,

    /// <summary>
    /// Composite chain of multiple transforms.
    /// </summary>
    Chained = 5,

    /// <summary>
    /// Unknown or external transform type. Falls back to interface dispatch.
    /// </summary>
    Other = 255
}
