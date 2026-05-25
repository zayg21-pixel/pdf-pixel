using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Transform;

/// <summary>
/// Converts CMYK (as <see cref="Vector4"/> X=C, Y=M, Z=Y, W=K) to approximate sRGB using a polynomial model.
/// </summary>
public sealed class CmykColorTransform : IColorTransform
{
    private static readonly Vector4 Rc = new(-0.01720446f, 0.21367942f, 0.07383375f, 0.83237892f);

    private const float Rc0 = -1.11856197f;

    private static readonly Vector4 Rm = new(0f, 0.00672540f, -0.02203832f, -0.07028028f);

    private const float Rm0 = -0.02155924f;

    private static readonly Vector4 Ry = new(0f, 0f, -0.00989797f, -0.08332911f);

    private const float Ry0 = 0.06867227f;

    private static readonly Vector4 Rk = new(0f, 0f, 0f, -0.08582930f);

    private const float Rk0 = -0.74306513f;

    private static readonly Vector4 R0 = new(Rc0, Rm0, Ry0, Rk0);

    private static readonly Vector4 Gc = new(0.03470997f, 0.23595206f, 0.02696618f, 0.12220627f);

    private const float Gc0 = -0.31136035f;

    private static readonly Vector4 Gm = new(0f, -0.06004063f, 0.06894216f, 0.51432357f);

    private const float Gm0 = -0.74890522f;

    private static readonly Vector4 Gy = new(0f, 0f, 0.01741545f, 0.03870308f);

    private const float Gy0 = -0.09771653f;

    private static readonly Vector4 Gk = new(0f, 0f, 0f, -0.08132402f);

    private const float Gk0 = -0.73649132f;

    private static readonly Vector4 G0 = new(Gc0, Gm0, Gy0, Gk0);

    private static readonly Vector4 Bc = new(0.00346864f, 0.03168226f, 0.12117562f, -0.00093778f);

    private const float Bc0 = -0.05563833f;

    private static readonly Vector4 Bm = new(0f, 0.04115760f, 0.24715288f, 0.19806650f);

    private const float Bm0 = -0.44015233f;

    private static readonly Vector4 By = new(0f, 0f, 0.00012926f, 0.45334939f);

    private const float By0 = -0.75914448f;

    private static readonly Vector4 Bk = new(0f, 0f, 0f, -0.08729634f);

    private const float Bk0 = -0.70635859f;

    private static readonly Vector4 B0 = new(Bc0, Bm0, By0, Bk0);

    /// <summary>
    /// Instance of <see cref="CmykColorTransform"/>.
    /// </summary>
    public static CmykColorTransform Instance { get; } = new();

    /// <inheritdoc />
    public bool IsIdentity => false;

    /// <summary>
    /// Transforms CMYK represented as vector to approximate sRGB using a polynomial model.
    /// </summary>
    /// <param name="color">CMYK color in range [0 - 1].</param>
    /// <returns>sRGB color.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Transform(Vector4 color)
    {
        float c = color.X;
        float m = color.Y;
        float y = color.Z;
        float k = color.W;

        Vector4 cmyk = color;

        Vector4 rWeights = R0 + (c * Rc) + (m * Rm) + (y * Ry) + (k * Rk);
        Vector4 gWeights = G0 + (c * Gc) + (m * Gm) + (y * Gy) + (k * Gk);
        Vector4 bWeights = B0 + (c * Bc) + (m * Bm) + (y * By) + (k * Bk);

        float r = ColorVectorUtilities.CustomDot(rWeights, cmyk);
        float g = ColorVectorUtilities.CustomDot(gWeights, cmyk);
        float b = ColorVectorUtilities.CustomDot(bWeights, cmyk);

        return Vector4.One + new Vector4(r, g, b, 0);
    }
}
