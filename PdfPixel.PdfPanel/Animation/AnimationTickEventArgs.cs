using System;

namespace PdfPixel.PdfPanel.Animation;

/// <summary>
/// Provides tick data for <see cref="PdfAnimationClock.Tick"/>.
/// </summary>
public sealed class AnimationTickEventArgs : EventArgs
{
    /// <summary>
    /// Initializes the event args with the current tick count.
    /// </summary>
    public AnimationTickEventArgs(long tick) => Tick = tick;

    /// <summary>
    /// Monotonically incrementing tick counter. Combine with <see cref="PdfAnimationClock.Fps"/> to derive animation values.
    /// </summary>
    public long Tick { get; }
}

