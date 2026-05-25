using PdfPixel.Color.Functions;
using PdfPixel.Color.Icc.Model;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Evaluator for PowerWithOffsetAndC TRC for 4 channels at once.
/// </summary>
internal sealed class PowerWithOffsetAndCTrcVectorEvaluator : IIccTrcVectorEvaluator
{
    private readonly FastPowSeriesDegree3Vector4 _pow;
    private readonly Vector4 _breakpoint;
    private readonly Vector4 _constantC;
    private readonly Vector4 _scale;
    private readonly Vector4 _offset;

    public PowerWithOffsetAndCTrcVectorEvaluator(IccTrcParameters[] parameters)
    {
        parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
        _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
        _constantC = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.ConstantC);
        _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
        _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
        Vector4 gammaVector = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Gamma);
        _pow = new FastPowSeriesDegree3Vector4(gammaVector);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector4 Evaluate(Vector4 x)
    {
        Vector4 mask = new(
            (x.X < _breakpoint.X) ? 1f : 0f,
            (x.Y < _breakpoint.Y) ? 1f : 0f,
            (x.Z < _breakpoint.Z) ? 1f : 0f,
            (x.W < _breakpoint.W) ? 1f : 0f);

        if (mask == Vector4.One)
        {
            return _constantC;
        }

        if (mask == Vector4.Zero)
        {
            return _pow.Evaluate((_scale * x) + _offset) + _constantC;
        }

        Vector4 linear = _constantC;
        Vector4 nonLinear = _pow.Evaluate((_scale * x) + _offset) + _constantC;
        return (mask * linear) + ((Vector4.One - mask) * nonLinear);
    }
}
