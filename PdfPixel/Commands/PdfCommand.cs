using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Base class for all PDF drawing commands, providing the disposable pattern.
/// </summary>
public abstract class PdfCommand : IPdfCommand
{
    /// <inheritdoc/>
    ~PdfCommand() => Dispose(disposing: false);

    /// <inheritdoc />
    public virtual void Initialize(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext)
    {
    }

    /// <inheritdoc />
    public abstract void Execute(IEnumerable<IPdfCommandModifier> modifiers, PdfCommandExecutionContext executionContext);

    /// <inheritdoc />
    public virtual bool IsScaleDependent => false;

    /// <summary>
    /// Releases resources owned by this command.
    /// </summary>
    /// <param name="disposing">True when called from <see cref="Dispose()"/>; false from the finalizer.</param>
    protected abstract void Dispose(bool disposing);

    /// <inheritdoc />
    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
