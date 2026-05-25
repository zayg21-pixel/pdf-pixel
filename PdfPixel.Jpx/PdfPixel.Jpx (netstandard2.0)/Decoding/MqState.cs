using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Jpx.Decoding;

/// <summary>
/// Represents a single row of the MQ probability estimation table (ITU-T T.800 Table C.3).
/// </summary>
internal readonly struct MqState
{
    /// <summary>
    /// Gets the Qe probability estimate for this state.
    /// </summary>
    public readonly ushort Qe;

    /// <summary>
    /// Gets the next state index when the MPS (most probable symbol) is decoded.
    /// </summary>
    public readonly byte NextMps;

    /// <summary>
    /// Gets the next-state index for an LPS event with the switch bit pre-OR'd into bit 7.
    /// Bit 7 is set when the MPS sense must be flipped on an LPS event (ITU-T T.800 Table C.3 SWITCH column).
    /// Combining both values into one byte lets <c>DecodeBit</c> compute the new context as a single XOR
    /// against the packed MPS byte, instead of a separate load, shift, and OR.
    /// </summary>
    public readonly byte NextLpsWithSwitch;

    /// <summary>
    /// Initializes a new instance of <see cref="MqState"/>.
    /// </summary>
    /// <param name="qe">Qe probability estimate.</param>
    /// <param name="nextMps">Next state index for an MPS event.</param>
    /// <param name="nextLpsWithSwitch">Next LPS state index OR'd with the switch bit in bit 7.</param>
    public MqState(ushort qe, byte nextMps, byte nextLpsWithSwitch)
    {
        Qe = qe;
        NextMps = nextMps;
        NextLpsWithSwitch = nextLpsWithSwitch;
    }
}
