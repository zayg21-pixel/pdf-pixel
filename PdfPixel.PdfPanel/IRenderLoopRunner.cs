using PdfPixel.PdfPanel.Requests;
using System;
using System.Threading;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Provides platform-specific control over the render loop execution and request queuing.
/// </summary>
public interface IRenderLoopRunner : IDisposable
{
    /// <summary>
    /// Starts the render loop, calling <paramref name="iteration"/> with the render frame each frame.
    /// The implementation controls timing, queuing, and yielding to the platform's event loop.
    /// </summary>
    /// <param name="iteration">The action to call for each render iteration with the render frame.</param>
    void Start(Action<PdfPanelRenderCommand> iteration);

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
