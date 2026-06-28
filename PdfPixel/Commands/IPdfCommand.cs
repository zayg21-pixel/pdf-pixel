using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Represents a single drawing or state-management operation on a canvas.
/// </summary>
public interface IPdfCommand : IDisposable
{
    /// <summary>
    /// Prepares this command for execution — decodes images, populates caches, and performs
    /// other long-running work that does not require the canvas. Called on the background thread
    /// before <see cref="Execute"/>. The execution context must not change between the two calls.
    /// </summary>
    /// <param name="modifiers">The modifiers to apply during execution, applied in order.</param>
    /// <param name="executionContext">Execution-time context containing rendering parameters and cancellation.</param>
    void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext);

    /// <summary>
    /// Executes this command. The canvas to draw on is obtained from <see cref="PdfCommandExecutionContext.Canvas"/>.
    /// </summary>
    /// <param name="modifiers">The modifiers to apply to paints before drawing, applied in order.</param>
    /// <param name="executionContext">Execution-time context containing the canvas, rendering parameters, and cancellation.</param>
    void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext);

    /// <summary>
    /// If true - command will produce different result depending on scale.
    /// </summary>
    bool IsScaleDependent { get; }
}
