using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Imaging;
using PdfPixel.Pattern;

namespace PdfPixel.Rendering.Image;

internal class PdfImageFillRenderTarget : IRenderTarget
{
    private readonly PdfImage _image;
    private readonly PdfGraphicsState _state;
    private readonly PdfPattern? _pattern;

    public PdfImageFillRenderTarget(PdfImage image, PdfGraphicsState state)
    {
        _image = image;
        _state = state;

        if (image.HasImageMask && state.FillPaint.IsPattern)
        {
            _pattern = state.FillPaint.Pattern;
        }
    }

    public PdfPaint Paint => _state.FillPaint;

    public void Render(IPdfCommandProcessor processor)
    {
        if (_pattern != null)
        {
            RenderPattern(processor, _pattern);
        }
        else
        {
            processor.Process(new DrawImageCommand(_image, CreateImagePaint(), null));
        }
    }

    private void RenderPattern(IPdfCommandProcessor processor, PdfPattern pattern)
    {
        PdfGroupSource? source = pattern.GetGroupSource(_state, _state.FillPaint);

        if (source != null)
        {
            processor.Process(new DrawImageCommand(_image, CreateImagePaint(), source));
        }
    }

    /// <summary>
    /// Returns the paint the image is drawn with: the fill paint unchanged for a stencil mask, otherwise the fill paint
    /// remapping device color spaces by the current resources (ISO 32000-2 8.6.5.6) with the image's rendering intent.
    /// </summary>
    private PdfPaint CreateImagePaint()
    {
        PdfPaint fillPaint = _state.FillPaint;

        if (_image.HasImageMask)
        {
            return fillPaint;
        }

        PdfRenderingIntent renderingIntent = _image.RenderingIntent ?? fillPaint.RenderingIntent;

        return fillPaint
            .WithDeviceColorMapping(_state.Host.Resources.DeviceColorMapping)
            .WithRenderingIntent(renderingIntent);
    }
}
