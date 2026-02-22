using PdfPixel.PdfPanel.Requests;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel;

public readonly struct RenderFrameCommand
{
    public RenderFrameCommand(PdfPanelRenderCommand command, CancellationToken cancellationToken)
    {
        Command = command;
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// The drawing command to process.
    /// </summary>
    public PdfPanelRenderCommand Command { get; }

    /// <summary>
    /// Cancellation token that is cancelled when a newer command arrives.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}

/// <summary>
/// Provides platform-specific control over the render loop execution and request queuing.
/// On browsers, implementations use <c>emscripten_set_main_loop</c> to yield to the event loop.
/// On desktop, the default blocking semaphore approach is used.
/// </summary>
public interface IRenderLoopRunner : IDisposable
{
    /// <summary>
    /// Starts the render loop, calling <paramref name="iteration"/> with the render frame each frame.
    /// The implementation controls timing, queuing, and yielding to the platform's event loop.
    /// </summary>
    /// <param name="iteration">The action to call for each render iteration with the render frame.</param>
    void Start(Action<RenderFrameCommand> iteration);

    /// <summary>
    /// Stops the render loop.
    /// </summary>
    void Stop();

    /// <summary>
    /// Enqueues a drawing request for processing. Cancels any in-progress frame rendering.
    /// </summary>
    /// <param name="request">The drawing request to enqueue.</param>
    void Enqueue(DrawingRequest request);
}
