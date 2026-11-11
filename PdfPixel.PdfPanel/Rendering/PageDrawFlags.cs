using System;

namespace PdfPixel.PdfPanel.Rendering;

[Flags]
internal enum PageDrawFlags
{
    Shadow = 1 << 0,
    Background = 1 << 1,
    Content = 1 << 2,
    Thumbnail = 1 << 3,
    AllContent = Shadow | Background | Thumbnail | Content
}
