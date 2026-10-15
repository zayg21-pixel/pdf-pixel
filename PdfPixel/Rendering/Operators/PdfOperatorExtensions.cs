using PdfPixel.Models;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Rendering.Operators;

internal static class PdfOperatorExtensions
{
    /// <summary>
    /// The operator an operator token names; <see cref="PdfOperator.Unknown"/> for a token longer than three bytes.
    /// A token of up to three bytes that names no operator yields a value with no <see cref="PdfOperator"/> member.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfOperator AsOperator(this in PdfString token)
    {
        ReadOnlySpan<byte> bytes = token.Value.Span;

        switch (bytes.Length)
        {
            case 1:
            {
                return (PdfOperator)bytes[0];
            }
            case 2:
            {
                return (PdfOperator)(bytes[0] | ((uint)bytes[1] << 8));
            }
            case 3:
            {
                return (PdfOperator)(bytes[0] | ((uint)bytes[1] << 8) | ((uint)bytes[2] << 16));
            }
            default:
            {
                return PdfOperator.Unknown;
            }
        }
    }
}
