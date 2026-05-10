using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Text;

namespace PdfPixel.Commands;

/// <summary>
/// Helper methods for commands, e.g. for applying modifiers to paints or paths.
/// </summary>
internal class CommandHelpers
{
    /// <summary>
    /// Applies the modifiers to the given paint, returning a new paint with the modifiers applied.
    /// </summary>
    public static SKPaint ApplyModifiers(SKPaint paint, IEnumerable<IPdfCommandModifier> modifiers)
    {
        var result = paint.Clone();

        foreach (var modifier in modifiers)
        {
            modifier.ModifyPaint(result);
        }

        return result;
    }

    public static bool GetPathIsAntialias(SKPath path, SKCanvas canvas, PdfCommandExecutionContext executionContext)
    {
        if (path.IsRect && canvas.TotalMatrix.SkewX == 0 && canvas.TotalMatrix.SkewY == 0)
        {
            return false;
        }

        return executionContext.RenderingParameters.Antialias;
    }
}
