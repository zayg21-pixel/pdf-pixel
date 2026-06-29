using System;

namespace PdfPixel.PdfPanel.Extensions;

[Flags]
internal enum PageDrawFlags
{
    Shadow = 1 << 0,
    Background = 1 << 1,
    Content = 1 << 2,
    Placeholder = 1 << 3,
    All = Shadow | Background | Content | Placeholder
}
