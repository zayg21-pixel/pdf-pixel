using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Pushes a marked content scope onto the execution context stack.
/// </summary>
public sealed class BeginMarkedContentCommand : PdfCommand
{
    /// <summary>
    /// Initializes a new <see cref="BeginMarkedContentCommand"/> with the specified marked content.
    /// </summary>
    public BeginMarkedContentCommand(PdfMarkedContent markedContent) => MarkedContent = markedContent;

    /// <summary>
    /// The marked content scope being entered.
    /// </summary>
    public PdfMarkedContent MarkedContent { get; }

    /// <inheritdoc />
    public override void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
        => executionContext.MarkedContent.Push(MarkedContent);

    /// <inheritdoc />
    protected override void Dispose(bool disposing)
    {
    }
}
