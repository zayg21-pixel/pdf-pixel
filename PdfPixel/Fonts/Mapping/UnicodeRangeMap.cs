using System;

namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Represents a contiguous mapping range from code bytes to Unicode code points.
/// The range is defined for a specific code byte length (1..4) and a start/end
/// of the code value interpreted as big-endian unsigned integers.
/// For a given input code value V where Start &lt;= V &lt;= End, the mapped Unicode
/// scalar value is computed as StartUnicode + (V - Start).
/// </summary>
internal readonly struct UnicodeRangeMap
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UnicodeRangeMap"/> struct.
    /// </summary>
    /// <param name="length">Code byte length this range applies to (1..4).</param>
    /// <param name="start">Inclusive start of the code value (big-endian).</param>
    /// <param name="end">Inclusive end of the code value (big-endian).</param>
    /// <param name="startUnicode">Unicode scalar start value for the mapping.</param>
    public UnicodeRangeMap(int length, uint start, uint end, int startUnicode)
    {
        Length = length;
        Start = start;
        End = end;
        StartUnicode = startUnicode;
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
    /// Unicode scalar start value for the mapping.
    /// </summary>
    public int StartUnicode { get; }
}
