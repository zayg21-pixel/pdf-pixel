using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a single drawing or state-management operation on a canvas.
/// </summary>
public interface IPdfCommand : IDisposable
{
    /// <summary>
    /// Executes this command against the specified canvas.
    /// </summary>
    /// <param name="canvas">The canvas to draw on.</param>
    /// <param name="modifiers">The modifiers to apply to paints before drawing, applied in order.</param>
    /// <param name="executionContext">Execution-time context containing rendering parameters and cancellation.</param>
    Task ExecuteAsync(SKCanvas canvas, IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext);

    /// <summary>
    /// If true - command will produce different result depending on scale.
    /// </summary>
    bool IsScaleDependant { get; }
}
