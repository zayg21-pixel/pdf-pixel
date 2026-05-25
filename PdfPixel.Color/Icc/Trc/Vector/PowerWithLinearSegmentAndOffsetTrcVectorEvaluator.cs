using PdfPixel.Color.Functions;
using PdfPixel.Color.Icc.Model;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

/// <summary>
/// Evaluator for PowerWithLinearSegmentAndOffset TRC for 4 channels at once.
/// </summary>
internal sealed class PowerWithLinearSegmentAndOffsetTrcVectorEvaluator : IIccTrcVectorEvaluator
{
    private readonly FastPowSeriesDegree3Vector4 _pow;
    private readonly Vector4 _breakpoint;
    private readonly Vector4 _constantC;
    private readonly Vector4 _scale;
    private readonly Vector4 _offset;
    private readonly Vector4 _powerOffset;
    private readonly Vector4 _linearOffset;

    public PowerWithLinearSegmentAndOffsetTrcVectorEvaluator(IccTrcParameters[] parameters)
    {
        parameters = IccTrcVectorEvaluatorHelpers.FillParams(parameters);
        _breakpoint = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Breakpoint);
        _constantC = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.ConstantC);
        _scale = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Scale);
        _offset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.Offset);
        _powerOffset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.PowerOffset);
        _linearOffset = IccTrcVectorEvaluatorHelpers.FromParameters(parameters, x => x.LinearOffset);
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
            return (_constantC * x) + _linearOffset;
        }

        if (mask == Vector4.Zero)
        {
            return _pow.Evaluate((_scale * x) + _offset) + _powerOffset;
        }

        Vector4 linear = (_constantC * x) + _linearOffset;
        Vector4 nonLinear = _pow.Evaluate((_scale * x) + _offset) + _powerOffset;
        return (mask * linear) + ((Vector4.One - mask) * nonLinear);
    }
}
