using System;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// How the destination samples along one axis map onto the source samples: the source position of each destination
/// sample's center, first plus step times its index, in samples of the source.
/// </summary>
public readonly struct PdfImageRowAxisMapping
{
    /// <summary>
    /// Initializes a mapping.
    /// </summary>
    /// <param name="sourceCount">The number of source samples along the axis.</param>
    /// <param name="destinationCount">The number of destination samples along the axis.</param>
    /// <param name="firstCenter">The source position of the first destination sample's center.</param>
    /// <param name="step">The source samples one destination sample spans; positive.</param>
    public PdfImageRowAxisMapping(int sourceCount, int destinationCount, float firstCenter, float step)
    {
        if (sourceCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sourceCount), "The source must hold at least one sample.");
        }

        if (destinationCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(destinationCount), "The destination must hold at least one sample.");
        }

        if (!(step > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(step), "The step must be positive.");
        }

        SourceCount = sourceCount;
        DestinationCount = destinationCount;
        FirstCenter = firstCenter;
        Step = step;
    }

    /// <summary>
    /// Gets the number of source samples along the axis.
    /// </summary>
    public int SourceCount { get; }

    /// <summary>
    /// Gets the number of destination samples along the axis.
    /// </summary>
    public int DestinationCount { get; }

    /// <summary>
    /// Gets the source position of the first destination sample's center.
    /// </summary>
    public float FirstCenter { get; }

    /// <summary>
    /// Gets the source samples one destination sample spans.
    /// </summary>
    public float Step { get; }

    /// <summary>
    /// Gets whether a destination sample spans less than one source sample, so that the axis grows.
    /// </summary>
    public bool Grows => Step < 1;

    /// <summary>
    /// Gets whether every destination sample is exactly one source sample, in order, all of them in the source.
    /// </summary>
    public bool IsOneToOne
    {
        get
        {
            return Step == 1
                && FirstCenter - Math.Floor(FirstCenter) == 0.5
                && FirstCenter > 0
                && FirstCenter - 0.5 + DestinationCount <= SourceCount;
        }
    }

    /// <summary>
    /// Returns the source position of the center of destination sample <paramref name="destinationIndex"/>.
    /// </summary>
    public float GetCenter(int destinationIndex) => FirstCenter + (destinationIndex * Step);

    /// <summary>
    /// Returns the source samples interpolating the destination samples reads in any
    /// <see cref="PdfImageRowInterpolationMode"/>: the two around each destination sample's center, clamped to the
    /// source.
    /// </summary>
    public PdfImageRowSampleRange GetInterpolationRange()
    {
        int start = ClampToSource((int)Math.Floor(FirstCenter - 0.5f));
        int end = ClampToSource((int)Math.Floor(GetCenter(DestinationCount - 1) - 0.5f) + 1) + 1;

        return new PdfImageRowSampleRange(start, end);
    }

    /// <summary>
    /// Returns the mapping that keeps the source samples of <paramref name="range"/> as they are, one destination
    /// sample each.
    /// </summary>
    public PdfImageRowAxisMapping GetSourceSamplesMapping(in PdfImageRowSampleRange range)
        => new(SourceCount, range.Count, range.Start + 0.5f, 1f);

    /// <summary>
    /// Returns the source sample whose cell holds the center of destination sample
    /// <paramref name="destinationIndex"/>, clamped to the source.
    /// </summary>
    public int GetNearestSource(int destinationIndex) => ClampToSource((int)Math.Floor(GetCenter(destinationIndex)));

    /// <summary>
    /// Returns the source samples whose centers lie in the half-open footprint of destination sample
    /// <paramref name="destinationIndex"/>, clamped to the source; the sample under its center when there is none.
    /// </summary>
    public PdfImageRowSampleRange GetFootprint(int destinationIndex)
    {
        float center = GetCenter(destinationIndex);
        float halfStep = Step * 0.5f;
        int start = Math.Max(0, (int)Math.Ceiling(center - halfStep - 0.5f));
        int end = Math.Min(SourceCount, (int)Math.Ceiling(center + halfStep - 0.5f));

        if (end <= start)
        {
            int nearest = GetNearestSource(destinationIndex);

            return new PdfImageRowSampleRange(nearest, nearest + 1);
        }

        return new PdfImageRowSampleRange(start, end);
    }

    private int ClampToSource(int sourceIndex) => Math.Max(0, Math.Min(SourceCount - 1, sourceIndex));
}
