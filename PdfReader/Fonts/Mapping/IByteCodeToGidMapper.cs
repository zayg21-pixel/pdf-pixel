namespace PdfReader.Fonts.Mapping
{
    public interface IByteCodeToGidMapper
    {
        ushort GetGid(byte code);
    }
}