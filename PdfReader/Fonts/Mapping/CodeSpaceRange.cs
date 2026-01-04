namespace PdfReader.Fonts.Mapping
{
    /// <summary>
    /// Defines a valid codespace interval for a particular code byte length.
    /// A codespace range identifies the inclusive Start/End bounds of code values,
    /// interpreted as big-endian unsigned integers, that are valid for the given Length.
    /// </summary>
    public readonly struct CodeSpaceRange
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CodeSpaceRange"/> struct.
        /// </summary>
        /// <param name="length">Code byte length (1..4).</param>
        /// <param name="start">Inclusive start of the valid code value interval.</param>
        /// <param name="end">Inclusive end of the valid code value interval.</param>
        public CodeSpaceRange(int length, uint start, uint end)
        {
            Length = length;
            Start = start;
            End = end;
        }

        /// <summary>
        /// Code byte length (1..4).
        /// </summary>
        public int Length { get; }

        /// <summary>
        /// Inclusive start of the valid code value interval (big-endian).
        /// </summary>
        public uint Start { get; }

        /// <summary>
        /// Inclusive end of the valid code value interval (big-endian).
        /// </summary>
        public uint End { get; }
    }
}
