using PdfPixel.Color.Functions;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Evaluator for gamma TRC using a vector of gamma values and FastPowSeriesDegree3Vector4.
/// </summary>
internal sealed class GammaTrcVectorEvaluator : IIccTrcVectorEvaluator
{
    private readonly FastPowSeriesDegree3Vector4 _pow;

    public GammaTrcVectorEvaluator(float[] gamma)
    {
        if (gamma == null || gamma.Length == 0 || gamma.Length > 4)
        {
            throw new ArgumentException("gamma must be an array of 1 to 4 floats", nameof(gamma));
        }

        var g = new float[4];
        for (int i = 0; i < 4; i++)
        {
            g[i] = (i < gamma.Length) ? gamma[i] : 1.0f;
        }

        _pow = new FastPowSeriesDegree3Vector4(new Vector4(g[0], g[1], g[2], g[3]));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Evaluate(Vector4 x) => _pow.Evaluate(x);
}
