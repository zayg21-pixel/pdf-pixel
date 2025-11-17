using PdfReader.Color.Paint;
using PdfReader.Rendering.State;
using SkiaSharp;

namespace PdfReader.Rendering.Path;

internal class PathFillRenderTarget : IRenderTarget
{
    private readonly SKPath _path;
    private readonly SKPaint _basePaint;
    private SKPicture _shaderPicture;
    private SKMatrix _shaderMatrix;

    public PathFillRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;

        if (state.FillPaint != null && state.FillPaint.IsPattern && state.FillPaint.Pattern != null)
        {
            _shaderMatrix = SKMatrix.Concat(state.CTM.Invert(), state.FillPaint.Pattern.PatternMatrix);
            _shaderPicture = state.FillPaint.Pattern.AsPicture(state); // cached, don't dispose!
            _basePaint = PdfPaintFactory.CreateShadingPaint(state);
        }
        else
        {
            _basePaint = PdfPaintFactory.CreateFillPaint(state);
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (_shaderPicture != null)
        {
            canvas.Save();
            canvas.ClipPath(_path, SKClipOperation.Intersect, antialias: true);

            canvas.Concat(_shaderMatrix);
            canvas.DrawPicture(_shaderPicture, _basePaint);

            canvas.Restore();
        }
        else
        {
            canvas.DrawPath(_path, _basePaint);
        }
    }

    public void Dispose()
    {
        _basePaint.Dispose();
        
    }
}

internal class PathStrokeRenderTarget : IRenderTarget
{
    private readonly SKPath _path;
    private readonly SKPaint _basePaint;
    private SKPicture _shaderPicture;
    private SKMatrix _shaderMatrix;

    public PathStrokeRenderTarget(SKPath path, PdfGraphicsState state)
    {
        _path = path;

        if (state.StrokePaint != null && state.StrokePaint.IsPattern && state.StrokePaint.Pattern != null)
        {
            _shaderMatrix = SKMatrix.Concat(state.CTM.Invert(), state.StrokePaint.Pattern.PatternMatrix);
            _shaderPicture = state.StrokePaint.Pattern.AsPicture(state); // cached, don't dispose!
            _basePaint = PdfPaintFactory.CreateShadingPaint(state);

            using var strokePaint = PdfPaintFactory.CreateStrokePaint(state);
            _path = _basePaint.GetFillPath(path);
        }
        else
        {
            _basePaint = PdfPaintFactory.CreateStrokePaint(state);
        }
    }

    public void Render(SKCanvas canvas)
    {
        if (_shaderPicture != null)
        {
            canvas.Save();
            canvas.ClipPath(_path, SKClipOperation.Intersect, antialias: true);

            canvas.Concat(_shaderMatrix);
            canvas.DrawPicture(_shaderPicture, _basePaint);

            canvas.Restore();
        }
        else
        {
            canvas.DrawPath(_path, _basePaint);
        }
    }

    public void Dispose()
    {
        _basePaint.Dispose();

    }
}