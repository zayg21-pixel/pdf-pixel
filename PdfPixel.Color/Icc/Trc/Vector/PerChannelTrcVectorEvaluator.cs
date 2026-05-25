using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Icc.Trc;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Evaluator for per-channel ICC TRC using individual IIccTrcEvaluator fields for each channel.
/// </summary>
internal sealed class PerChannelTrcVectorEvaluator : IIccTrcVectorEvaluator
{
    private readonly IIccTrcEvaluator _evaluator0;
    private readonly IIccTrcEvaluator _evaluator1;
    private readonly IIccTrcEvaluator _evaluator2;
    private readonly IIccTrcEvaluator _evaluator3;

    public PerChannelTrcVectorEvaluator(IccTrc[] trcs)
    {
        if (trcs == null || trcs.Length == 0 || trcs.Length > 4)
        {
            throw new ArgumentException("trcs must be an array of 1 to 4 IccTrc", nameof(trcs));
        }

        _evaluator0 = (trcs.Length > 0) ? IccTrcEvaluatorFactory.Create(trcs[0]) : IccTrcEvaluatorFactory.Create(null);
        _evaluator1 = (trcs.Length > 1) ? IccTrcEvaluatorFactory.Create(trcs[1]) : IccTrcEvaluatorFactory.Create(null);
        _evaluator2 = (trcs.Length > 2) ? IccTrcEvaluatorFactory.Create(trcs[2]) : IccTrcEvaluatorFactory.Create(null);
        _evaluator3 = (trcs.Length > 3) ? IccTrcEvaluatorFactory.Create(trcs[3]) : IccTrcEvaluatorFactory.Create(null);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Evaluate(Vector4 x)
    {
        float r = _evaluator0.Evaluate(x.X);
        float g = _evaluator1.Evaluate(x.Y);
        float b = _evaluator2.Evaluate(x.Z);
        float a = _evaluator3.Evaluate(x.W);
        return new Vector4(r, g, b, a);
    }
}
