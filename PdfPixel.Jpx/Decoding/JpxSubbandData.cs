using PdfPixel.Jpx.Model;
using PdfPixel.Jpx.Parsing;
using System;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Reusable coefficient storage for one component's subband decomposition.
/// </summary>
/// <remarks>
/// A dyadic wavelet decomposition of a width x height component occupies exactly
/// width * height coefficients in total, so every subband is carved out of a single
/// backing buffer. <see cref="Reset"/> re-lays out that buffer for a new component
/// size, growing it only when a larger component is encountered, which lets one
/// instance serve every tile and component of an image.
/// </remarks>
internal sealed class JpxSubbandData
{
    private readonly int[] _offsets;
    private readonly int[] _widths;
    private readonly int[] _heights;

    private int[] _coefficients = [];
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
    public int GetResolutionWidth(int levels)
        => JpxBandGeometry.GetResolutionCoordinate(_tileBounds.Right, levels) - JpxBandGeometry.GetResolutionCoordinate(_tileBounds.X, levels);

    /// <summary>
    /// Height of the component reconstructed down to <paramref name="levels"/> decomposition levels.
    /// </summary>
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
    /// LL subband (lowest resolution) coefficients.
    /// </summary>
    public Span<int> LL => _coefficients.AsSpan(_offsets[0], _widths[0] * _heights[0]);

    /// <summary>
    /// Lays the buffer out for a component of the given size and clears it, so subbands
    /// no code-block contributes to read as zero.
    /// </summary>
    /// <param name="tileBounds">Bounds of the component's tile on the reference grid.</param>
    public void Reset(in JpxRectangle tileBounds)
    {
        _tileBounds = tileBounds;

        int offset = 0;

        // Subband bounds come from the reference grid, so a tile that does not start on a
        // multiple of 2^Levels still lines up with the partitions the encoder used.
        SetSlot(0, GetResolutionWidth(Levels), GetResolutionHeight(Levels), ref offset);

        for (int level = 0; level < Levels; level++)
        {
            int bandLevel = level + 1;

            SetSlot(GetSlot(level, JpxSubbandType.HL), GetBandWidth(bandLevel, 1), GetBandHeight(bandLevel, 0), ref offset);
            SetSlot(GetSlot(level, JpxSubbandType.LH), GetBandWidth(bandLevel, 0), GetBandHeight(bandLevel, 1), ref offset);
            SetSlot(GetSlot(level, JpxSubbandType.HH), GetBandWidth(bandLevel, 1), GetBandHeight(bandLevel, 1), ref offset);
        }

        if (_coefficients.Length < offset)
        {
            _coefficients = new int[offset];
        }

        _usedLength = offset;
        _coefficients.AsSpan(0, _usedLength).Clear();
    }

    /// <summary>
    /// Gets the coefficients of a detail subband at the specified level.
    /// </summary>
    public Span<int> GetSubband(int level, JpxSubbandType type)
    {
        int slot = GetSlot(level, type);
        return _coefficients.AsSpan(_offsets[slot], _widths[slot] * _heights[slot]);
    }

    /// <summary>
    /// Gets the width of a detail subband at the specified level.
    /// </summary>
    public int GetWidth(int level, JpxSubbandType type) => _widths[GetSlot(level, type)];

    /// <summary>
    /// Gets the height of a detail subband at the specified level.
    /// </summary>
    public int GetHeight(int level, JpxSubbandType type) => _heights[GetSlot(level, type)];

    private void SetSlot(int slot, int width, int height, ref int offset)
    {
        _offsets[slot] = offset;
        _widths[slot] = width;
        _heights[slot] = height;
        offset += width * height;
    }

    private static int GetSlot(int level, JpxSubbandType type) => 1 + (level * 3) + (int)type;

    private int GetBandWidth(int bandLevel, int bandOffset)
        => JpxBandGeometry.GetBandCoordinate(_tileBounds.Right, bandLevel, bandOffset) - JpxBandGeometry.GetBandCoordinate(_tileBounds.X, bandLevel, bandOffset);

    private int GetBandHeight(int bandLevel, int bandOffset)
        => JpxBandGeometry.GetBandCoordinate(_tileBounds.Bottom, bandLevel, bandOffset) - JpxBandGeometry.GetBandCoordinate(_tileBounds.Y, bandLevel, bandOffset);
}
