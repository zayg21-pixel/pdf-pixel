namespace PdfPixel.PdfPanel.Animation;

/// <summary>
/// Captures the current animation tick and frame rate for use in draw calls.
/// </summary>
public readonly struct AnimationState
{
    /// <summary>
    /// Initializes the state from a clock's current values.
    /// </summary>
    public AnimationState(long tick, int fps)
    {
        Tick = tick;
        Fps = fps;
    }

    /// <summary>
    /// Monotonically incrementing tick counter from <see cref="PdfAnimationClock"/>.
    /// </summary>
    public long Tick { get; }

    /// <summary>
    /// Frames per second the clock is running at.
    /// </summary>
    public int Fps { get; }
}

