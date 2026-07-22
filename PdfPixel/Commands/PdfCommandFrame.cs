using PdfPixel.Geometry;

namespace PdfPixel.Commands;

/// <summary>
/// A saved matrix/clip state on <see cref="PdfCommandExecutionFrames"/>'s save stack, restored on
/// the matching <see cref="PdfCommandExecutionFrames.OnRestoreState"/>.
/// </summary>
public readonly struct PdfCommandFrame
{
    /// <summary>
    /// Initializes the frame with the given matrix, clip, and layer flag.
    /// </summary>
    public PdfCommandFrame(in PdfMatrix matrix, PdfClipState? clip, bool isLayer)
    {
        Matrix = matrix;
        Clip = clip;
        IsLayer = isLayer;
    }

    /// <summary>
    /// Gets the total transformation matrix saved at this frame.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets the current clip saved at this frame, or null when no clip was active.
    /// </summary>
    public PdfClipState? Clip { get; }

    /// <summary>
    /// Gets whether this frame was pushed by <see cref="PdfCommandExecutionFrames.OnSaveLayer"/>.
    /// </summary>
    public bool IsLayer { get; }
}
