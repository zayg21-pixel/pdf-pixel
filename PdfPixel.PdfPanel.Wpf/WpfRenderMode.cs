namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Specifies the rendering backend used by <see cref="WpfPdfPanel"/>.
/// </summary>
public enum WpfRenderMode
{
    /// <summary>
    /// CPU-only rendering via SkiaSharp raster surfaces, presented through a
    /// <see cref="System.Windows.Media.Imaging.WriteableBitmap"/>.
    /// </summary>
    Software,

    /// <summary>
    /// GPU-accelerated rendering via Direct3D 12, presented through a
    /// <see cref="System.Windows.Interop.D3DImage"/> surface chain
    /// (D3D12 → D3D11 → D3D9Ex).
    /// </summary>
    Direct3D,

    /// <summary>
    /// Experimental GPU-accelerated rendering via OpenGL (WGL), with pixel readback
    /// to a <see cref="System.Windows.Media.Imaging.WriteableBitmap"/> for presentation.
    /// Avoids the D3D shared-context memory issues while still leveraging GPU drawing.
    /// </summary>
    OpenGl
}
