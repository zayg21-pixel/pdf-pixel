namespace PdfRender.Imaging.Ccitt
{
    /// <summary>
    /// Represents a single CCITT run-length code (terminating, make-up, or EOL).
    /// </summary>
    internal readonly struct CcittFaxCode
    {
        public CcittFaxCode(int bitLength, int code, int runLength, bool isMakeUp = false, bool isEndOfLine = false)
        {
            BitLength = bitLength;
            Code = code;
            RunLength = runLength;
            IsMakeUp = isMakeUp;
            IsEndOfLine = isEndOfLine;
        }

        public int BitLength { get; }

        public int Code { get; }

        public int RunLength { get; }

        public bool IsMakeUp { get; }

        public bool IsEndOfLine { get; }
    }
}
