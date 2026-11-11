using PdfPixel.Geometry;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// A transparency group an element is painted with: a recording drawn as a group, optionally tiled.
/// </summary>
public sealed class PdfGroupSource
{
    /// <summary>
    /// Initializes the source with the recording drawn as the group, the group it is drawn as, the area it is limited to
    /// and its tiling.
    /// </summary>
    /// <param name="recording">The recording drawn as the group, with its matrix and tint.</param>
    /// <param name="group">The group the recording is drawn as.</param>
    /// <param name="bounds">The area the group is limited to, in the space the source is painted in, or <see langword="null"/> when it is not limited.</param>
    /// <param name="tiling">How the recording repeats, or <see langword="null"/> when it is drawn once.</param>
    public PdfGroupSource(DrawRecordingCommand recording, PdfTransparencyGroupContext group, PdfRectangle? bounds, PdfTiling? tiling)
    {
        Recording = recording ?? throw new ArgumentNullException(nameof(recording));
        Group = group ?? throw new ArgumentNullException(nameof(group));
        Tiling = tiling;

        Bounds = bounds;

        if (tiling == null)
        {
            PdfRectangle? contentBounds = recording.Recorder.GetBounds();

            if (contentBounds != null)
            {
                Bounds = PaintBounds.Intersect(bounds, recording.Matrix.MapRect(contentBounds.Value));
            }
        }
    }

    /// <summary>
    /// Gets the recording drawn as the group, with its matrix and tint.
    /// </summary>
    public DrawRecordingCommand Recording { get; }

    /// <summary>
    /// Gets the group the recording is drawn as.
    /// </summary>
    public PdfTransparencyGroupContext Group { get; }

    /// <summary>
    /// Gets the area the group's content paints within the area it is limited to, in the space the source is painted in,
    /// or <see langword="null"/> when it is not bounded.
    /// </summary>
    public PdfRectangle? Bounds { get; }

    /// <summary>
    /// Gets how the recording repeats, or <see langword="null"/> when it is drawn once.
    /// </summary>
    public PdfTiling? Tiling { get; }

    /// <inheritdoc />
    public override string ToString()
        => $"group{((Group.Isolated) ? " isolated" : string.Empty)}{((Group.Knockout) ? " knockout" : string.Empty)}{((Tiling != null) ? " tiled" : string.Empty)}";
}
