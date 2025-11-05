namespace PdfReader.Fonts.Mapping
{
    internal class OneToOneCodeToGidMapper : IByteCodeToGidMapper
    {
        private readonly ushort _offset;

        public OneToOneCodeToGidMapper(ushort offset = 0)
        {
            _offset = offset;
        }
        public ushort GetGid(byte code)
        {
            return (ushort)(code + _offset);
        }
    }
}