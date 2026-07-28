using PdfPixel.Jpx.Model;
using System;

namespace PdfPixel.Jpx.Parsing;

/// <summary>
/// Packet enumeration for the progression orders that scan the reference grid by position
/// before resolution, per ITU-T T.800 B.12.1.4 and B.12.1.5.
/// </summary>
/// <remarks>
/// These orders cannot be enumerated by stepping precinct indices. A precinct index means a
/// different place on the reference grid at every resolution, because each resolution has its
/// own precinct size and its own scale, so walking indices interleaves the resolutions in an
/// order no encoder produces. The grid position is walked instead, and a packet is emitted for
/// a component and resolution only where that position starts one of its precincts.
/// </remarks>
internal static class JpxPositionProgression
{
    /// <summary>
    /// Computes the step between the grid positions worth visiting, which is the smallest
    /// precinct extent on the reference grid across every resolution of the components in
    /// <paramref name="firstComponent"/>..<paramref name="lastComponent"/>.
    /// Returns zero for either axis when no resolution contributes a usable extent.
    /// </summary>
    public static (int stepX, int stepY) ComputeStep(
        JpxHeader header,
        int firstComponent,
        int lastComponent)
    {
        if (header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        int resolutionCount = header.CodingStyle.DecompositionLevels + 1;
        int stepX = 0;
        int stepY = 0;

        for (int component = firstComponent; component <= lastComponent; component++)
        {
            int separationX = GetHorizontalSeparation(header, component);
            int separationY = GetVerticalSeparation(header, component);

            for (int resolution = 0; resolution < resolutionCount; resolution++)
            {
                (int exponentX, int exponentY) = JpxPrecinctHelper.GetPrecinctExponents(resolution, header.CodingStyle);
                int levels = resolutionCount - 1 - resolution;

                stepX = ReduceStep(stepX, separationX, exponentX + levels);
                stepY = ReduceStep(stepY, separationY, exponentY + levels);
            }
        }

        return (stepX, stepY);
    }

    /// <summary>
    /// Determines whether the packet for <paramref name="component"/> and
    /// <paramref name="resolution"/> starts at grid position
    /// (<paramref name="positionX"/>, <paramref name="positionY"/>), and if so which precinct
    /// of that resolution it belongs to.
    /// </summary>
    public static bool TryGetPrecinctAt(
        JpxHeader header,
        in JpxRectangle tileBounds,
        int component,
        int resolution,
        int positionX,
        int positionY,
        out int precinctX,
        out int precinctY)
    {
        precinctX = 0;
        precinctY = 0;

        if (header.CodingStyle == null)
        {
            throw new InvalidOperationException("Coding style is not defined.");
        }

        int resolutionCount = header.CodingStyle.DecompositionLevels + 1;
        int levels = resolutionCount - 1 - resolution;

        long scaleX = (long)GetHorizontalSeparation(header, component) << levels;
        long scaleY = (long)GetVerticalSeparation(header, component) << levels;

        long resolutionX0 = CeilDivide(tileBounds.X, scaleX);
        long resolutionY0 = CeilDivide(tileBounds.Y, scaleY);
        long resolutionX1 = CeilDivide(tileBounds.Right, scaleX);
        long resolutionY1 = CeilDivide(tileBounds.Bottom, scaleY);

        // A resolution the tile contributes no samples to has no packets at any position.
        if (resolutionX0 == resolutionX1 || resolutionY0 == resolutionY1)
        {
            return false;
        }

        (int exponentX, int exponentY) = JpxPrecinctHelper.GetPrecinctExponents(resolution, header.CodingStyle);

        if (!StartsPrecinct(positionX, tileBounds.X, resolutionX0, scaleX, exponentX, levels)
            || !StartsPrecinct(positionY, tileBounds.Y, resolutionY0, scaleY, exponentY, levels))
        {
            return false;
        }

        precinctX = (int)(FloorShift(CeilDivide(positionX, scaleX), exponentX) - FloorShift(resolutionX0, exponentX));
        precinctY = (int)(FloorShift(CeilDivide(positionY, scaleY), exponentY) - FloorShift(resolutionY0, exponentY));

        return true;
    }

    /// <summary>
    /// Whether a precinct of this resolution starts at <paramref name="position"/> along one axis.
    /// A precinct starts either where the position falls on the precinct partition, or at the
    /// tile's own edge when the partition cuts the first precinct short there.
    /// </summary>
    private static bool StartsPrecinct(
        int position,
        int tileStart,
        long resolutionStart,
        long scale,
        int exponent,
        int levels)
    {
        long partitionStep = scale << exponent;

        if (position % partitionStep == 0)
        {
            return true;
        }

        return position == tileStart && ((resolutionStart << levels) % (1L << (exponent + levels))) != 0;
    }

    /// <summary>
    /// Folds one resolution's precinct extent into the running minimum, ignoring extents too
    /// large to represent.
    /// </summary>
    private static int ReduceStep(int step, int separation, int shift)
    {
        if (shift >= 31)
        {
            return step;
        }

        long candidate = (long)separation << shift;

        if (candidate > int.MaxValue)
        {
            return step;
        }

        return (step == 0) ? (int)candidate : Math.Min(step, (int)candidate);
    }

    private static int GetHorizontalSeparation(JpxHeader header, int component)
        => (component < header.Components.Count) ? Math.Max((int)header.Components[component].HorizontalSeparation, 1) : 1;

    private static int GetVerticalSeparation(JpxHeader header, int component)
        => (component < header.Components.Count) ? Math.Max((int)header.Components[component].VerticalSeparation, 1) : 1;

    private static long CeilDivide(long value, long divisor) => (value + divisor - 1) / divisor;

    private static long FloorShift(long value, int shift) => value >> shift;
}
