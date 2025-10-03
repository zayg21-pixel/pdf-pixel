namespace PdfReader.Icc
{
    internal struct IccXyz
    {
        public readonly float X;
        public readonly float Y;
        public readonly float Z;
        public IccXyz(float x, float y, float z)
        {
            X = x; Y = y; Z = z;
        }
        public override string ToString() => $"({X}, {Y}, {Z})";
    }
}
