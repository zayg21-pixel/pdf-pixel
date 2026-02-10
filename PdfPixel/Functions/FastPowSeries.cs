using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Functions;

/// <summary>
/// Chebyshev-series based fast power approximation for fixed alpha over all positive x, vectorization-friendly.
/// Assumes inputs are normal finite positive floats; minimizes operations for this range only.
/// Maps x into m in [0.5,1) via exponent/mantissa split, then approximates f(m)=m^alpha on [-1,1] using Chebyshev T_k.
/// </summary>
internal static class FastPowSeries
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static unsafe int FloatToIntBits(float value)
    {
        return *(int*)&value;
    }

    /// <summary>
    /// Build Chebyshev coefficients c[0..N] for any function f(x) on interval [a, b].
    /// Uses Chebyshev nodes for optimal approximation properties.
    /// </summary>
    /// <param name="function">Function to approximate</param>
    /// <param name="intervalStart">Start of approximation interval [a, b]</param>
    /// <param name="intervalEnd">End of approximation interval [a, b]</param>
    /// <param name="degree">Degree of Chebyshev approximation</param>
    /// <returns>Chebyshev coefficients c[0..degree]</returns>
    public static float[] BuildChebyshevCoeffs(Func<float, float> function, float intervalStart, float intervalEnd, int degree)
    {
        if (degree < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degree));
        }
        if (function == null)
        {
            throw new ArgumentNullException(nameof(function));
        }
        if (intervalStart >= intervalEnd)
        {
            throw new ArgumentException("Interval start must be less than interval end");
        }

        int n = degree;
        float[] c = new float[n + 1];

        int mNodes = Math.Max(2 * (n + 1), 64);
        float[] fvals = new float[mNodes];

        // Map domain [a, b] to [-1, 1] for Chebyshev nodes
        float intervalMid = 0.5f * (intervalStart + intervalEnd);
        float intervalHalfWidth = 0.5f * (intervalEnd - intervalStart);

        for (int j = 0; j < mNodes; j++)
        {
            float theta = (j + 0.5f) * (float)Math.PI / mNodes;
            float z = MathF.Cos(theta); // Chebyshev node in [-1, 1]
            float x = intervalMid + intervalHalfWidth * z; // Map to [a, b]
            fvals[j] = function(x);
        }

        // Compute Chebyshev coefficients using discrete cosine transform
        float sum0 = 0.0f;
        for (int j = 0; j < mNodes; j++)
        {
            sum0 += fvals[j];
        }
        c[0] = sum0 / mNodes;

        for (int k = 1; k <= n; k++)
        {
            float sk = 0.0f;
            for (int j = 0; j < mNodes; j++)
            {
                float theta = (j + 0.5f) * (float)Math.PI / mNodes;
                sk += fvals[j] * MathF.Cos(k * theta);
            }
            c[k] = (2.0f / mNodes) * sk;
        }

        return c;
    }

    /// <summary>
    /// Build scaling lookup table indexed by exponent bits: scale[expBits] = MathF.Pow(twoNegAlpha, 126 - expBits).
    /// Covers all possible expBits (0..255) for IEEE 754 float.
    /// </summary>
    public static float[] BuildScaleLut(float alpha)
    {
        const int Size = 256; // indices 0..255, covering all possible expBits
        float[] scale = new float[Size];
        float twoNegAlpha = MathF.Pow(0.5f, alpha);
        for (int expBits = 0; expBits < Size; expBits++)
        {
            scale[expBits] = MathF.Pow(twoNegAlpha, 126 - expBits);
        }
        return scale;
    }

    /// <summary>
    /// Build Horner-form coefficients directly for evaluating m^alpha as p0 + m*(p1 + m*(p2 + ...)) on m in [0.5,1].
    /// This is mathematically equivalent to building Chebyshev coefficients and then transforming them,
    /// but computes the Horner coefficients directly without the intermediate step.
    /// </summary>
    public static float[] BuildHornerCoeffsForMantissa(float alpha, int degree)
    {
        return BuildHornerCoeffs(m => MathF.Pow(m, alpha), 0.5f, 1.0f, degree);
    }

    /// <summary>
    /// Build Horner-form polynomial coefficients for any function f(x) on interval [a, b].
    /// Returns coefficients p[0..degree] such that p[0] + x*p[1] + x²*p[2] + ... approximates f(x).
    /// The polynomial is optimized for evaluation in the original interval [a, b].
    /// </summary>
    /// <param name="function">Function to approximate</param>
    /// <param name="intervalStart">Start of approximation interval [a, b]</param>
    /// <param name="intervalEnd">End of approximation interval [a, b]</param>
    /// <param name="degree">Degree of polynomial approximation</param>
    /// <returns>Horner-form coefficients p[0..degree]</returns>
    public static float[] BuildHornerCoeffs(Func<float, float> function, float intervalStart, float intervalEnd, int degree)
    {
        if (degree < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(degree));
        }
        if (function == null)
        {
            throw new ArgumentNullException(nameof(function));
        }
        if (intervalStart >= intervalEnd)
        {
            throw new ArgumentException("Interval start must be less than interval end");
        }

        // First compute Chebyshev coefficients on the interval
        float[] chebyshevCoeffs = BuildChebyshevCoeffs(function, intervalStart, intervalEnd, degree);

        // Transform to Horner form with appropriate domain mapping
        // For interval [a, b], we need to map x ∈ [a, b] to z ∈ [-1, 1]
        // z = 2*(x - a)/(b - a) - 1 = (2/(b-a))*x + (-(a+b)/(b-a))
        float intervalWidth = intervalEnd - intervalStart;
        float a_coeff = 2.0f / intervalWidth;
        float b_coeff = -(intervalStart + intervalEnd) / intervalWidth;

        return TransformChebyshevToHorner(chebyshevCoeffs, a_coeff, b_coeff);
    }

    /// <summary>
    /// Transform Chebyshev coefficients to Horner-form polynomial coefficients for general linear mapping.
    /// Given c[0..N] for sum(c[k] * T_k(a*x + b)),
    /// returns p[0..N] such that the polynomial p[0] + x*p[1] + x²*p[2] + ... + x^N*p[N]
    /// is equivalent to the Chebyshev series evaluated at z = a*x + b.
    /// </summary>
    private static float[] TransformChebyshevToHorner(float[] chebyshevCoeffs, float a, float b)
    {
        int n = chebyshevCoeffs.Length - 1;
        float[] hornerCoeffs = new float[n + 1];

        // For each Chebyshev polynomial T_k(a*x + b), expand it as a polynomial in x
        // and accumulate the coefficients weighted by c[k]
        
        for (int k = 0; k <= n; k++)
        {
            if (MathF.Abs(chebyshevCoeffs[k]) < 1e-15f)
            {
                continue; // Skip negligible coefficients
            }

            // Get polynomial coefficients for T_k(a*x + b) as powers of x
            float[] tkCoeffs = GetChebyshevPolynomialCoeffs(k, a, b);
            
            // Add c[k] * T_k(a*x + b) to the result
            for (int j = 0; j < tkCoeffs.Length && j <= n; j++)
            {
                hornerCoeffs[j] += chebyshevCoeffs[k] * tkCoeffs[j];
            }
        }

        return hornerCoeffs;
    }

    /// <summary>
    /// Transform Chebyshev coefficients to Horner-form polynomial coefficients.
    /// Given c[0..N] for sum(c[k] * T_k(z)) where z = 4*m - 3,
    /// returns p[0..N] such that the polynomial p[0] + m*p[1] + m²*p[2] + ... + m^N*p[N]
    /// is equivalent to the Chebyshev series evaluated at z = 4*m - 3.
    /// </summary>
    private static float[] TransformChebyshevToHorner(float[] chebyshevCoeffs)
    {
        return TransformChebyshevToHorner(chebyshevCoeffs, 4.0f, -3.0f);
    }

    /// <summary>
    /// Get polynomial coefficients for T_k(a*x + b) expressed as powers of x.
    /// Returns coefficients [c0, c1, c2, ...] such that T_k(a*x + b) = c0 + c1*x + c2*x² + ...
    /// </summary>
    private static float[] GetChebyshevPolynomialCoeffs(int k, float a, float b)
    {
        if (k == 0)
        {
            return [1.0f]; // T_0 = 1
        }
        if (k == 1)
        {
            return [b, a]; // T_1(ax + b) = ax + b
        }

        // Use recurrence: T_{k+1}(x) = 2*x*T_k(x) - T_{k-1}(x)
        // For T_k(ax + b), we substitute x -> ax + b and expand
        
        float[] result = new float[k + 1];
        
        // Start with T_0 and T_1 coefficients for (ax + b)
        float[] t0 = { 1.0f }; // T_0 = 1
        float[] t1 = { b, a };  // T_1 = ax + b
        
        if (k == 1)
        {
            return t1;
        }

        float[] prev2 = t0;
        float[] prev1 = t1;
        
        for (int i = 2; i <= k; i++)
        {
            float[] current = new float[i + 1];
            
            // T_i(ax + b) = 2*(ax + b)*T_{i-1}(ax + b) - T_{i-2}(ax + b)
            
            // First term: 2*(ax + b)*T_{i-1}
            // Multiply prev1 by (ax + b) = b + a*x
            for (int j = 0; j < prev1.Length; j++)
            {
                // Coefficient of x^j in prev1 contributes to:
                // - b * x^j (constant term b)
                // - a * x^{j+1} (linear term a*x)
                if (j < current.Length)
                {
                    current[j] += 2.0f * b * prev1[j];
                }
                if (j + 1 < current.Length)
                {
                    current[j + 1] += 2.0f * a * prev1[j];
                }
            }
            
            // Second term: -T_{i-2}
            for (int j = 0; j < prev2.Length && j < current.Length; j++)
            {
                current[j] -= prev2[j];
            }
            
            prev2 = prev1;
            prev1 = current;
        }
        
        return prev1;
    }
}
