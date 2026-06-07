using System;

namespace PdfPixel.Commands;

/// <summary>
/// Bridge adapter that immediately executes commands against the canvas held in the execution context.
/// Used at system boundaries (e.g. <see cref="PdfPixel.Rendering.PdfContentStreamRenderer"/>)
/// where a real canvas is available, enabling the rest of the pipeline to work with
/// <see cref="IPdfCommandProcessor"/> uniformly.
/// </summary>
public sealed class SkCanvasCommandProcessor : IPdfCommandProcessor
{
    private readonly PdfCommandExecutionContext _executionContext;

    /// <summary>
    /// Initializes the processor with the given execution context.
    /// The canvas to draw on is obtained from <see cref="PdfCommandExecutionContext.Canvas"/>.
    /// </summary>
    public SkCanvasCommandProcessor(PdfCommandExecutionContext executionContext) => _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        command.Execute(Array.Empty<IPdfCommandModifier>(), _executionContext);
        _executionContext.CurrentCommand = command;
        command.Dispose();
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Does not own the execution context; caller manages its lifetime.
    }
}
