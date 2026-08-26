using PdfPixel.Color.Paint;
using PdfPixel.Commands.Context;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands.Processing;

/// <summary>
/// The region one path command covers as it lands on the device pixel grid: the geometry to fill — the
/// path itself, or the outline the pen leaves along it — snapped to whole device pixels where it can be,
/// and whether it is drawn with antialiasing.
/// </summary>
internal readonly struct PdfPathDeviceGeometry
{
    // Device-pixel thickness above which a mark keeps its shape with its edges snapped to the grid.
    private const float MinimumAliasedThickness = 2f;

    // Caps and joins a degenerate fill is stroked with. Its line width comes from the hairline pen.
    private static readonly PdfStrokeStyle HairlineStrokeStyle = new();

    // Held as whichever it came out as: a path left where it was, or the rectangle it was snapped to.
    private readonly PdfPath? _path;
    private readonly PdfRectangle _rectangle;
    private readonly PdfPathFillType _fillType;

    private PdfPathDeviceGeometry(PdfPath path, bool isStrokeOutline, bool isAntialias)
    {
        _path = path;
        _rectangle = PdfRectangle.Empty;
        _fillType = path.FillType;
        SnappedRectangle = null;
        IsStrokeOutline = isStrokeOutline;
        IsAntialias = isAntialias;
    }

    private PdfPathDeviceGeometry(in PdfRectangle rectangle, PdfPathFillType fillType, PdfRectangle? snappedRectangle, bool isStrokeOutline, bool isAntialias)
    {
        _path = null;
        _rectangle = rectangle;
        _fillType = fillType;
        SnappedRectangle = snappedRectangle;
        IsStrokeOutline = isStrokeOutline;
        IsAntialias = isAntialias;
    }

    /// <summary>
    /// The rectangle the geometry was snapped to, or <see langword="null"/> when it was left where it was.
    /// </summary>
    public PdfRectangle? SnappedRectangle { get; }

    /// <summary>
    /// Whether the geometry is a pen outline to be filled rather than a path to be stroked along.
    /// </summary>
    public bool IsStrokeOutline { get; }

    /// <summary>
    /// Whether the geometry is drawn with coverage.
    /// </summary>
    public bool IsAntialias { get; }

    /// <summary>
    /// Builds the region <paramref name="path"/> covers when drawn with <paramref name="paint"/>.
    /// </summary>
    public static PdfPathDeviceGeometry CreateForDrawing(PdfPath path, PdfPaint paint, PdfCommandExecutionContext executionContext)
    {
        if (paint.Style == PdfPaintStyle.Stroke)
        {
            return CreateStroke(path, paint, executionContext);
        }

        PdfPathInfo pathInfo = path.GetPathInfo();

        // A fill with zero-width or zero-height bounds covers no area; a hairline pen keeps it visible.
        if (pathInfo.Bounds.Width == 0 || pathInfo.Bounds.Height == 0)
        {
            return CreateStroke(path, HairlineStrokeStyle, PdfDevicePen.Create(executionContext, 0f), executionContext);
        }

        return CreateFill(path, pathInfo, executionContext);
    }

    /// <summary>
    /// Builds the region <paramref name="path"/> covers when clipped to under <paramref name="paint"/>:
    /// the pen outline when the paint strokes, and the path itself otherwise. A clip covering no area
    /// clips everything away and gets no hairline.
    /// </summary>
    public static PdfPathDeviceGeometry CreateForClipping(PdfPath path, PdfPaint? paint, PdfCommandExecutionContext executionContext)
    {
        if (paint != null && paint.Style == PdfPaintStyle.Stroke)
        {
            return CreateStroke(path, paint, executionContext);
        }

        return CreateFill(path, path.GetPathInfo(), executionContext);
    }

    /// <summary>
    /// Builds the region <paramref name="rectangle"/> covers when clipped to, for a command that carries
    /// a rectangle rather than a path.
    /// </summary>
    public static PdfPathDeviceGeometry CreateForClipping(in PdfRectangle rectangle, PdfCommandExecutionContext executionContext)
    {
        PdfMatrix deviceMatrix = executionContext.Frames.TotalMatrix;
        PdfPathInfo rectangleInfo = new(rectangle, isRectilinear: true, isRectangle: true);

        return Create(sourcePath: null, rectangleInfo, GetDeviceFillThickness(rectangleInfo, deviceMatrix), isStrokeOutline: false, deviceMatrix, executionContext);
    }

    /// <summary>
    /// Returns the geometry to fill, in the space the command gave its path in.
    /// </summary>
    public PdfPath GetPath()
    {
        if (_path != null)
        {
            return _path;
        }

        PdfPathBuilder builder = new();
        builder.AddRect(_rectangle);

        return builder.ToPath(_fillType);
    }

    private static PdfPathDeviceGeometry CreateFill(PdfPath path, in PdfPathInfo pathInfo, PdfCommandExecutionContext executionContext)
    {
        PdfMatrix deviceMatrix = executionContext.Frames.TotalMatrix;

        return Create(path, pathInfo, GetDeviceFillThickness(pathInfo, deviceMatrix), isStrokeOutline: false, deviceMatrix, executionContext);
    }

    private static PdfPathDeviceGeometry CreateStroke(PdfPath path, PdfPaint paint, PdfCommandExecutionContext executionContext)
    {
        PdfStrokeStyle strokeStyle = paint.RequireStrokeStyle();

        return CreateStroke(path, strokeStyle, PdfDevicePen.Create(executionContext, strokeStyle.LineWidth), executionContext);
    }

    private static PdfPathDeviceGeometry CreateStroke(PdfPath path, PdfStrokeStyle strokeStyle, in PdfDevicePen pen, PdfCommandExecutionContext executionContext)
    {
        PdfMatrix deviceMatrix = executionContext.Frames.TotalMatrix;
        PdfPath outline = PdfStrokeOutlineBuilder.BuildOutline(path, strokeStyle.WithLineWidth(pen.Width), pen.Matrix, deviceMatrix);

        return Create(outline, outline.GetPathInfo(), pen.DeviceThickness, isStrokeOutline: true, deviceMatrix, executionContext);
    }

    private static PdfPathDeviceGeometry Create(
        PdfPath? sourcePath,
        in PdfPathInfo geometryInfo,
        float deviceThickness,
        bool isStrokeOutline,
        in PdfMatrix deviceMatrix,
        PdfCommandExecutionContext executionContext)
    {
        bool isOnGrid = geometryInfo.IsRectilinear && PdfCommandProcessingUtilities.IsGridPreserving(deviceMatrix);
        PdfPathFillType fillType = (sourcePath != null) ? sourcePath.FillType : PdfPathFillType.Winding;

        // Only a rectangle can be snapped whole: any other shape holds members thinner than its bounds,
        // which rounding to those bounds would swallow.
        if (isOnGrid && geometryInfo.IsRectangle && executionContext.Parameters.SnapToDevicePixels)
        {
            PdfRectangle snappedRectangle = PdfCommandProcessingUtilities.SnapToWholeDevicePixels(geometryInfo.Bounds, deviceMatrix);

            return new PdfPathDeviceGeometry(snappedRectangle, fillType, snappedRectangle, isStrokeOutline, isAntialias: false);
        }

        bool isAntialias = executionContext.Parameters.Antialias
            && (!isOnGrid || deviceThickness < MinimumAliasedThickness);

        if (sourcePath == null)
        {
            return new PdfPathDeviceGeometry(geometryInfo.Bounds, fillType, snappedRectangle: null, isStrokeOutline, isAntialias);
        }

        return new PdfPathDeviceGeometry(sourcePath, isStrokeOutline, isAntialias);
    }

    // Device-pixel thickness of a fill. Anything but a single rectangle covers less than its bounds and
    // reports no thickness of its own.
    private static float GetDeviceFillThickness(in PdfPathInfo geometryInfo, in PdfMatrix deviceMatrix)
    {
        if (!geometryInfo.IsRectangle)
        {
            return 0f;
        }

        PdfRectangle deviceBounds = deviceMatrix.MapRect(geometryInfo.Bounds);

        return MathF.Min(deviceBounds.Width, deviceBounds.Height);
    }
}
