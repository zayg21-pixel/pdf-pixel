using PdfPixel.Commands;
using PdfPixel.Commands.Context;
using PdfPixel.Commands.Model;
using PdfPixel.Geometry;
using System;
using System.Threading.Tasks;

namespace PdfPixel.TextExtraction;

/// <summary>
/// Collects the characters of PDF commands into the text block tree of an execution context without drawing anything.
/// </summary>
public sealed class PdfTextExtractionCommandProcessor : IPdfCommandProcessor
{
    private readonly PdfCommandExecutionContext _executionContext;

    /// <summary>
    /// Initializes the processor with the execution context whose text block tree receives the characters.
    /// </summary>
    public PdfTextExtractionCommandProcessor(PdfCommandExecutionContext executionContext)
        => _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));

    /// <inheritdoc />
    public void Process(IPdfCommand command)
    {
        if (command == null)
        {
            throw new ArgumentNullException(nameof(command));
        }

        _executionContext.ExecutionObserver.Notify();

        if (!_executionContext.MarkedContent.ShouldExecute(command))
        {
            return;
        }

        switch (command.Kind)
        {
            case PdfCommandKind.BeginMarkedContent:
            {
                _executionContext.MarkedContent.Push(((BeginMarkedContentCommand)command).MarkedContent);
                break;
            }
            case PdfCommandKind.EndMarkedContent:
            {
                _executionContext.MarkedContent.Pop();
                break;
            }
            case PdfCommandKind.ConcatMatrix:
            {
                _executionContext.Frames.OnConcatMatrix(((ConcatMatrixCommand)command).Matrix);
                break;
            }
            case PdfCommandKind.SaveState:
            {
                _executionContext.Frames.OnSaveState();
                break;
            }
            case PdfCommandKind.SaveLayer:
            {
                _executionContext.Frames.OnSaveLayer();
                break;
            }
            case PdfCommandKind.RestoreState:
            case PdfCommandKind.RestoreLayer:
            {
                _executionContext.Frames.OnRestoreState();
                break;
            }
            case PdfCommandKind.TextCharacters:
            {
                var textCharactersCommand = (TextCharactersCommand)command;
                PdfMatrix matrix = _executionContext.Frames.TotalMatrix.PreConcat(textCharactersCommand.Matrix);
                _executionContext.MarkedContent.AppendCharacters(matrix, textCharactersCommand.Characters);
                break;
            }
        }
    }

    /// <inheritdoc />
    public ValueTask ProcessAsync(IPdfCommand command)
    {
        Process(command);

        return default;
    }
}
