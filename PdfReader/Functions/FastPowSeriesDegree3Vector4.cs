using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfReader.Functions;

/// <summary>
/// Experimental Vector4 version of the degree-3 Chebyshev approximation for x^alpha over positive finite floats.
/// Each lane in the input can have a different alpha. Horner evaluation is vectorized using Vector4 coefficients.
/// Designed to avoid APIs beyond .NET Standard 2.0.
/// </summary>
internal sealed class FastPowSeriesDegree3Vector4
{
    private readonly Vector4 _p0;
    private readonly Vector4 _p1;
    private readonly Vector4 _p2;
    private readonly Vector4 _p3;

    // Per-lane scaling LUTs: scale[expBits] = (0.5^alpha)^(126 - expBits)
    private readonly float[] _scaleX;
    private readonly float[] _scaleY;
    private readonly float[] _scaleZ;
    private readonly float[] _scaleW;

    private const float FractionScale = 1.0f / (1 << 23);
    private const float Half = 0.5f;

    /// <summary>
    /// Initialize Vector4 approximation for per-lane alphas.
    /// </summary>
    /// <param name="alpha">Exponent values (Vector4), one per lane.</param>
    public FastPowSeriesDegree3Vector4(Vector4 alpha)
    {
        float[] px = FastPowSeries.BuildHornerCoeffsForMantissa(alpha.X, degree: 3);
        float[] py = FastPowSeries.BuildHornerCoeffsForMantissa(alpha.Y, degree: 3);
        float[] pz = FastPowSeries.BuildHornerCoeffsForMantissa(alpha.Z, degree: 3);
        float[] pw = FastPowSeries.BuildHornerCoeffsForMantissa(alpha.W, degree: 3);

        _p0 = new Vector4(px[0], py[0], pz[0], pw[0]);
        _p1 = new Vector4(px[1], py[1], pz[1], pw[1]);
        _p2 = new Vector4(px[2], py[2], pz[2], pw[2]);
        _p3 = new Vector4(px[3], py[3], pz[3], pw[3]);

        // Reuse shared LUT builder
        _scaleX = FastPowSeries.BuildScaleLut(alpha.X);
        _scaleY = FastPowSeries.BuildScaleLut(alpha.Y);
        _scaleZ = FastPowSeries.BuildScaleLut(alpha.Z);
        _scaleW = FastPowSeries.BuildScaleLut(alpha.W);
    }

    /// <summary>
    /// Evaluate x^alpha per lane. Assumes inputs are positive finite floats in (0, 1] for best accuracy.
    /// Horner polynomial over mantissa is computed with Vector4 operations.
    /// </summary>
    /// <param name="x">Input values (Vector4), expected in (0, 1] per lane.</param>
    /// <returns>Approximation of x^alpha per lane.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Evaluate(Vector4 x)
    {
        // Extract mantissa and exponent bits per lane (scalar bit ops)
        ref int intValue = ref Unsafe.As<Vector4, int>(ref x);

        int bitsX = intValue;
        int exponentBitsX = (bitsX >> 23) & 0xFF;
        int fractionBitsX = bitsX & 0x7FFFFF;

        intValue = ref Unsafe.Add(ref intValue, 1);
        int bitsY = intValue;
        int exponentBitsY = (bitsY >> 23) & 0xFF;
        int fractionBitsY = bitsY & 0x7FFFFF;

        intValue = ref Unsafe.Add(ref intValue, 1);
        int bitsZ = intValue;
        int exponentBitsZ = (bitsZ >> 23) & 0xFF;
        int fractionBitsZ = bitsZ & 0x7FFFFF;

        intValue = ref Unsafe.Add(ref intValue, 1);
        int bitsW = intValue;
        int exponentBitsW = (bitsW >> 23) & 0xFF;
        int fractionBitsW = bitsW & 0x7FFFFF;

        // Vectorize mantissa mapping: m = (1 + frac * FractionScale) * 0.5
        Vector4 fraction = new Vector4(fractionBitsX, fractionBitsY, fractionBitsZ, fractionBitsW);
        Vector4 mantissa = (Vector4.One + fraction * FractionScale) * Half;

        // Vectorized Horner: p0 + m * (p1 + m * (p2 + m * p3))
        Vector4 inner = _p2 + mantissa * _p3;
        Vector4 mid = _p1 + mantissa * inner;
        Vector4 mantissaPoly = _p0 + mantissa * mid;

        // Per-lane scale via precomputed LUTs
        Vector4 scale = new Vector4(
            _scaleX[exponentBitsX],
            _scaleY[exponentBitsY],
            _scaleZ[exponentBitsZ],
            _scaleW[exponentBitsW]);

        return scale * mantissaPoly;
    }
}
