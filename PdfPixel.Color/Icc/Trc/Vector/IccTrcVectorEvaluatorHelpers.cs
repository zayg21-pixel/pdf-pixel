using PdfPixel.Color.Icc.Model;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;

namespace PdfPixel.Color.Icc.Trc.Vector;

internal static class IccTrcVectorEvaluatorHelpers
{
    public static IccTrcParameters IdentityParams { get; } = new();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static IccTrcParameters[] FillParams(IccTrcParameters[] parameters)
    {
        var arr = new IccTrcParameters[4];
        for (int i = 0; i < 4; i++)
        {
            arr[i] = (parameters != null && i < parameters.Length && parameters[i] != null)
                ? parameters[i]
                : IdentityParams;
        }

        return arr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector4 FromParameters(IccTrcParameters[] parameters, Func<IccTrcParameters, float> func)
        => new(func(parameters[0]), func(parameters[1]), func(parameters[2]), func(parameters[3]));
}
