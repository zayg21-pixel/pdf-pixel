using System;

namespace PdfPixel.Fonts.Model;

[Flags]
public enum PdfFontFlags
{
    None = 0,
    FixedPitch = 1,
    Serif = 1 << 1,
    Symbolic = 1 << 2,
    Script = 1 << 3,
    Nonsymbolic = 1 << 5,
    Italic = 1 << 6,
    AllCap = 1 << 16,
    SmallCap = 1 << 17,
    ForceBold = 1 << 18
}
