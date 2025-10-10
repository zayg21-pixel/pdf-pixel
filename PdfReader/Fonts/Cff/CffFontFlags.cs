using System;

namespace PdfReader.Fonts.Cff
{
    [Flags]
    public enum CffFontFlags
    {
        None = 0,
        FixedPitch = 1,
        Serif = 2,
        Symbolic = 4,
        Script = 8,
        Nonsymbolic = 32,
        Italic = 64,
        AllCap = 65536,
        SmallCap = 131072,
        ForceBold = 262144
    }
}