namespace PdfReader.Rendering.Image.Ccitt
{
    public enum ModeType
    {
        Pass,
        Horizontal,
        Vertical
    }

    public readonly struct ModeCode
    {
        public ModeCode(int bits, int code, ModeType type, int delta)
        {
            Bits = bits;
            Code = code;
            Type = type;
            VerticalDelta = delta;
        }

        public int Bits { get; }
        public int Code { get; }
        public ModeType Type { get; }
        public int VerticalDelta { get; }
    }
}
