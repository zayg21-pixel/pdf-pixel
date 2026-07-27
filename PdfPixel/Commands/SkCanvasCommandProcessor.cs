using Microsoft.Extensions.Logging;
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
    private readonly ILogger<SkCanvasCommandProcessor> _logger;

    /// <summary>
    /// Initializes the processor with the given execution context and logger.
    /// The canvas to draw on is obtained from <see cref="PdfCommandExecutionContext.Canvas"/>.
    /// </summary>
    public SkCanvasCommandProcessor(PdfCommandExecutionContext executionContext, ILogger<SkCanvasCommandProcessor> logger)
    {
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        if (!_executionContext.MarkedContent.ShouldExecute(command))
        {
            return;
        }

        try
        {
            command.Execute(_executionContext);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception exception)
#pragma warning restore CA1031 // Do not catch general exception types
        {
            _logger.LogWarning(exception, "Command {CommandType} failed during execution.", command.GetType().Name);
            return;
        }

        _executionContext.CurrentCommand = command;
    }
}
