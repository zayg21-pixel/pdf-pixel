using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Layout of one component's subband decomposition inside the component's sample buffer.
/// </summary>
/// <remarks>
/// Each level occupies the top-left corner of the buffer: its low-pass rows above its
/// high-pass rows, and within each row its low-pass columns before its high-pass ones.
/// The LL subband sits at the origin; HL, LH and HH fill the other three quadrants.
/// </remarks>
internal sealed class JpxSubbandData
{
    private readonly int[] _offsets;
    private readonly int[] _widths;
    private readonly int[] _heights;

    private int[] _samples = [];
    private int _usedLength;
    private JpxRectangle _tileBounds;

    /// <summary>
    /// Creates storage able to hold a decomposition of the given depth.
    /// </summary>
    /// <param name="levels">Number of DWT decomposition levels.</param>
    public JpxSubbandData(int levels)
    {
        Levels = levels;

        // Slot 0 is the LL subband; each level contributes its HL, LH and HH subbands.
        int slotCount = 1 + (levels * 3);
        _offsets = new int[slotCount];
        _widths = new int[slotCount];
        _heights = new int[slotCount];
    }

    /// <summary>
    /// Number of DWT decomposition levels.
    /// </summary>
    public int Levels { get; }

    /// <summary>
    /// Full reconstructed width of the component.
    /// </summary>
    public int FullWidth => _tileBounds.Width;

    /// <summary>
    /// Full reconstructed height of the component.
    /// </summary>
    public int FullHeight => _tileBounds.Height;

    /// <summary>
    /// Width of the component reconstructed down to <paramref name="levels"/> decomposition levels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResolutionWidth(int levels)
        => JpxBandGeometry.GetResolutionCoordinate(_tileBounds.Right, levels) - JpxBandGeometry.GetResolutionCoordinate(_tileBounds.X, levels);

    /// <summary>
    /// Height of the component reconstructed down to <paramref name="levels"/> decomposition levels.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetResolutionHeight(int levels)
        => JpxBandGeometry.GetResolutionCoordinate(_tileBounds.Bottom, levels) - JpxBandGeometry.GetResolutionCoordinate(_tileBounds.Y, levels);

    /// <summary>
    /// Reference-grid column the component starts at once reduced by <paramref name="levels"/>
    /// decomposition levels. Its parity decides whether the first sample of that resolution is
    /// a low-pass or a high-pass one.
    /// </summary>
    public int GetResolutionStartX(int levels)
        => JpxBandGeometry.GetResolutionCoordinate(_tileBounds.X, levels);

    /// <summary>
    /// Reference-grid row the component starts at once reduced by <paramref name="levels"/>
    /// decomposition levels.
    /// </summary>
    public int GetResolutionStartY(int levels)
        => JpxBandGeometry.GetResolutionCoordinate(_tileBounds.Y, levels);

    /// <summary>
    /// Width of the LL subband.
    /// </summary>
    public int LLWidth => _widths[0];

    /// <summary>
    /// Height of the LL subband.
    /// </summary>
    public int LLHeight => _heights[0];

    /// <summary>
    /// Distance between the starts of two consecutive rows of <see cref="Samples"/>.
    /// </summary>
    public int Stride { get; private set; }

    /// <summary>
    /// Component samples holding the subband coefficients, row-major with <see cref="Stride"/>.
    /// </summary>
    public Span<int> Samples => _samples.AsSpan(0, _usedLength);

    /// <summary>
    /// Lays the subbands out in <paramref name="samples"/> for a component of the given size.
    /// </summary>
    /// <param name="tileBounds">Bounds of the component's tile on the reference grid.</param>
    /// <param name="samples">Component sample buffer, zeroed, of at least the reconstructed size.</param>
    /// <param name="levelsToSkip">
    /// Finest decomposition levels the transform will not reconstruct. Their subbands hold the
    /// bulk of the coefficients, and neither the entropy decoder nor the transform touches them,
    /// so they are laid out empty rather than reserved.
    /// </param>
    public void Reset(in JpxRectangle tileBounds, int[] samples, int levelsToSkip = 0)
    {
        _tileBounds = tileBounds;
        _samples = samples ?? throw new ArgumentNullException(nameof(samples));
        Stride = GetResolutionWidth(levelsToSkip);
        _usedLength = Stride * GetResolutionHeight(levelsToSkip);

        // Subband bounds come from the reference grid, so a tile that does not start on a
        // multiple of 2^Levels still lines up with the partitions the encoder used.
        SetSlot(0, 0, 0, GetResolutionWidth(Levels), GetResolutionHeight(Levels));

        for (int level = 0; level < Levels; level++)
        {
            if (level < levelsToSkip)
            {
                SetSlot(GetSlot(level, JpxSubbandType.HL), 0, 0, 0, 0);
                SetSlot(GetSlot(level, JpxSubbandType.LH), 0, 0, 0, 0);
                SetSlot(GetSlot(level, JpxSubbandType.HH), 0, 0, 0, 0);

                continue;
            }

            int bandLevel = level + 1;
            int lowColumns = GetResolutionWidth(bandLevel);
            int lowRows = GetResolutionHeight(bandLevel);

            SetSlot(GetSlot(level, JpxSubbandType.HL), lowColumns, 0, GetBandWidth(bandLevel, 1), GetBandHeight(bandLevel, 0));
            SetSlot(GetSlot(level, JpxSubbandType.LH), 0, lowRows, GetBandWidth(bandLevel, 0), GetBandHeight(bandLevel, 1));
            SetSlot(GetSlot(level, JpxSubbandType.HH), lowColumns, lowRows, GetBandWidth(bandLevel, 1), GetBandHeight(bandLevel, 1));
        }
    }

    /// <summary>
    /// Gets the position in <see cref="Samples"/> of the first coefficient of a detail subband
    /// at the specified level.
    /// </summary>
    public int GetOffset(int level, JpxSubbandType type) => _offsets[GetSlot(level, type)];

    /// <summary>
    /// Gets the width of a detail subband at the specified level.
    /// </summary>
    public int GetWidth(int level, JpxSubbandType type) => _widths[GetSlot(level, type)];

    /// <summary>
    /// Gets the height of a detail subband at the specified level.
    /// </summary>
    public int GetHeight(int level, JpxSubbandType type) => _heights[GetSlot(level, type)];

    private void SetSlot(int slot, int x, int y, int width, int height)
    {
        _offsets[slot] = (y * Stride) + x;
        _widths[slot] = width;
        _heights[slot] = height;
    }

    private static int GetSlot(int level, JpxSubbandType type) => 1 + (level * 3) + (int)type;

    private int GetBandWidth(int bandLevel, int bandOffset)
        => JpxBandGeometry.GetBandCoordinate(_tileBounds.Right, bandLevel, bandOffset) - JpxBandGeometry.GetBandCoordinate(_tileBounds.X, bandLevel, bandOffset);

    private int GetBandHeight(int bandLevel, int bandOffset)
        => JpxBandGeometry.GetBandCoordinate(_tileBounds.Bottom, bandLevel, bandOffset) - JpxBandGeometry.GetBandCoordinate(_tileBounds.Y, bandLevel, bandOffset);
}
