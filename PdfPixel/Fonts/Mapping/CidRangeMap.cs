using System;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Represents a contiguous mapping range from code bytes to CID values.
/// The range is defined for a specific code byte length (1..4) and a start/end
/// of the code value interpreted as big-endian unsigned integers.
/// For a given input code value V where Start &lt;= V &lt;= End, the mapped CID
/// is computed as StartCid + (V - Start).
/// </summary>
internal readonly struct CidRangeMap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CidRangeMap"/> struct.
    /// </summary>
    /// <param name="length">Code byte length this range applies to (1..4).</param>
    /// <param name="start">Inclusive start of the code value (big-endian).</param>
    /// <param name="end">Inclusive end of the code value (big-endian).</param>
    /// <param name="startCid">CID start value for the mapping.</param>
    public CidRangeMap(int length, uint start, uint end, int startCid)
    {
        Length = length;
        Start = start;
        End = end;
        StartCid = startCid;
    }

    /// <summary>
    /// Code byte length this range applies to (1..4).
    /// </summary>
    public int Length { get; }

    /// <summary>
    /// Inclusive start of the code value (big-endian).
    /// </summary>
    public uint Start { get; }

    /// <summary>
    /// Inclusive end of the code value (big-endian).
    /// </summary>
    public uint End { get; }

    /// <summary>
    /// CID start value for the mapping.
    /// </summary>
    public int StartCid { get; }
}
