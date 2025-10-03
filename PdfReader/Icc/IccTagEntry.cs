namespace PdfReader.Icc
{
    internal sealed class IccTagEntry
    {
        public uint Signature { get; }
        public int Offset { get; }
        public int Size { get; }
        public string SignatureString => BigEndianReader.FourCCToString(Signature);

        public IccTagEntry(uint signature, int offset, int size)
        {
            Signature = signature;
            Offset = offset;
            Size = size;
        }
    }
}
