using System.Collections.Generic;
using System.Threading;

namespace PdfPixel.PdfPanel.Requests;

/// <summary>
/// Groups a queue of render commands with a cancellation token.
/// Skippable entries share a <see cref="CancellationToken"/> that is cancelled when newer
/// work is enqueued. Non-skippable entries use <see cref="CancellationToken.None"/>.
/// </summary>
public sealed class RequestEntry
{
    /// <summary>
    /// Creates a new <see cref="RequestEntry"/>.
    /// </summary>
    /// <param name="commands">The queue of commands to process.</param>
    /// <param name="cancellationToken">Token that indicates whether this request has been superseded.</param>
    public RequestEntry(Queue<PdfPanelRenderCommand> commands, CancellationToken cancellationToken)
    {
        Commands = commands;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Commands remaining to be processed for this request.
    /// </summary>
    public Queue<PdfPanelRenderCommand> Commands { get; }

    /// <summary>
    /// Token that indicates whether this request has been superseded.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
