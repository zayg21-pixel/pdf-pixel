using System.Runtime.CompilerServices;

namespace PdfPixel.Functions;

/// <summary>
/// Degree-3 Chebyshev approximation of x^alpha for positive finite floats, using exponent/mantissa split and a scaling LUT.
/// Cache-friendly and vectorization-friendly. Construct once per alpha and reuse.
/// </summary>
internal sealed class FastPowSeriesDegree3
{
    private readonly float[] _scaleLut; // scale[expBits] = pow(0.5, alpha)^(126 - expBits)

    // Horner-form coefficients for direct evaluation as p0 + m*(p1 + m*(p2 + m*p3)).
    private readonly float _p0;
    private readonly float _p1;
    private readonly float _p2;
    private readonly float _p3;
    private readonly bool _isOddIntegerAlpha;

    /// <summary>
    /// Initialize the degree-3 Chebyshev approximation for the given alpha.
    /// </summary>
    /// <param name="alpha">Exponent for x^alpha.</param>
    public FastPowSeriesDegree3(float alpha)
    {
        _scaleLut = FastPowSeries.BuildScaleLut(alpha);

        // Build Horner coefficients directly without intermediate Chebyshev coefficients
        float[] hornerCoeffs = FastPowSeries.BuildHornerCoeffsForMantissa(alpha, degree: 3);
        
        _p0 = hornerCoeffs[0];
        _p1 = hornerCoeffs[1];
        _p2 = hornerCoeffs[2];
        _p3 = hornerCoeffs[3];

        // Determine if alpha is an odd integer
        int intAlpha = (int)alpha;
        _isOddIntegerAlpha = alpha == intAlpha && (intAlpha & 1) == 1;
    }

    /// <summary>
    /// Evaluate x^alpha for positive finite floats using the cached Horner coefficients and scaling LUT.
    /// For negative x, preserves sign if alpha is an odd integer; otherwise returns the principal value (positive result).
    /// </summary>
    /// <param name="x">Input value. Should be a positive finite float for optimal accuracy.</param>
    /// <returns>Approximation of x^alpha.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public float Evaluate(float x)
    {
        // Extract bits of x
        int bits = FastPowSeries.FloatToIntBits(x);

        // Use absolute value bits for exponent/mantissa extraction
        int absBits = bits & 0x7FFFFFFF;
        int expBits = (absBits >> 23) & 0xFF; // 1..126 for (0,1]
        int fracBits = absBits & 0x7FFFFF;    // 23-bit fraction

        // Map mantissa to m in [0.5,1]
        float m = (1.0f + fracBits * (1.0f / (1 << 23))) * 0.5f;

        // Horner in m: p0 + m * (p1 + m * (p2 + m * p3))
        float mant = _p0 + m * (_p1 + m * (_p2 + m * _p3));

        float scale = _scaleLut[expBits];

        float result = scale * mant;

        // Only preserve sign for odd integer alpha
        if (_isOddIntegerAlpha && bits >> 31 != 0)
        {
            return -result;
        }
        return result;
    }
}