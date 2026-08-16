using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using System;

namespace PdfPixel.Commands;

// TODO: over-documented
/// <summary>
/// <para>
/// The region one path command covers, as it lands on the device pixel grid: the geometry to fill — the
/// path itself, or the outline the pen leaves along it — put on whole device pixels where it can go
/// there, and whether what comes out still needs coverage to carry the edges the grid cannot.
/// </para>
/// <para>
/// Filling, stroking and clipping all reach that decision here, from the same two facts: the shape of
/// the geometry that will be painted, and how thick a mark it leaves in device pixels. A stroke and the
/// band a producer clipped it to therefore land on the same row of pixels rather than on neighbouring
/// ones.
/// </para>
/// </summary>
internal readonly struct PdfPathDeviceGeometry
{
    // A mark at least this thick in device pixels keeps its shape when its edges are put on the grid.
    // A thinner one has only coverage left to carry it.
    private const float MinimumAliasedThickness = 2f;

    // The pen a degenerate fill is painted with. Its width comes from the hairline pen, so the width
    // here carries no meaning; the caps and joins are the ones such a fill is stroked with.
    private static readonly PdfStrokeStyle HairlineStrokeStyle = new();

    // The geometry, held as whichever of the two it came out as: a path left where it was, or the
    // rectangle it was put on. A command that fills a rectangle directly then costs no path at all.
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
    /// The whole device pixels the geometry was put on, or null when it was left where it was. A caller
    /// that can fill a rectangle without a path takes it from here.
    /// </summary>
    public PdfRectangle? SnappedRectangle { get; }

    /// <summary>
    /// Whether the geometry is the outline of a pen rather than the path a paint fills, so that a paint
    /// drawing it has to fill that outline rather than stroke along it.
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

        // A fill whose bounds are zero-width or zero-height covers no area at all, as the degenerate
        // "re f" rectangles some producers use to draw grid lines do. Drawing it with a pen one device
        // pixel wide is what keeps it visible.
        if (pathInfo.Bounds.Width == 0 || pathInfo.Bounds.Height == 0)
        {
            return CreateStroke(path, HairlineStrokeStyle, PdfDevicePen.Create(executionContext, 0f), executionContext);
        }

        return CreateFill(path, pathInfo, executionContext);
    }

    /// <summary>
    /// Builds the region <paramref name="path"/> covers when clipped to under <paramref name="paint"/>:
    /// the outline of the pen when the paint strokes, and the path itself when it fills or when there is
    /// no paint at all. A clip covering no area clips everything away and is not rescued, unlike a fill.
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
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);
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
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);

        return Create(path, pathInfo, GetDeviceFillThickness(pathInfo, deviceMatrix), isStrokeOutline: false, deviceMatrix, executionContext);
    }

    private static PdfPathDeviceGeometry CreateStroke(PdfPath path, PdfPaint paint, PdfCommandExecutionContext executionContext)
    {
        PdfStrokeStyle strokeStyle = paint.RequireStrokeStyle();

        return CreateStroke(path, strokeStyle, PdfDevicePen.Create(executionContext, strokeStyle.LineWidth), executionContext);
    }

    private static PdfPathDeviceGeometry CreateStroke(PdfPath path, PdfStrokeStyle strokeStyle, in PdfDevicePen pen, PdfCommandExecutionContext executionContext)
    {
        PdfMatrix deviceMatrix = CommandHelpers.GetScaledMatrix(executionContext);
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
        // Geometry that runs along the axes in its own space still does in device space under a matrix
        // that keeps the axes on the grid, which is what lets the two be decided apart here.
        bool isOnGrid = geometryInfo.IsRectilinear && CommandHelpers.IsGridPreserving(deviceMatrix);
        PdfPathFillType fillType = (sourcePath != null) ? sourcePath.FillType : PdfPathFillType.Winding;

        // Only a rectangle can be moved onto the grid whole: every other shape holds members thinner
        // than its bounds, which rounding to those bounds would swallow. Once the edges sit on pixel
        // boundaries there is no fractional coverage left for antialiasing to describe.
        if (isOnGrid && geometryInfo.IsRectangle && executionContext.Parameters.SnapToDevicePixels)
        {
            PdfRectangle snappedRectangle = SnapToWholeDevicePixels(geometryInfo.Bounds, deviceMatrix);

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

    // Puts the geometry on whole device pixels and brings it back to the space it was given in. The
    // rounding sends each edge to the pixel nearest it and nothing more, so that geometry abutting this
    // keeps abutting it.
    private static PdfRectangle SnapToWholeDevicePixels(in PdfRectangle bounds, in PdfMatrix deviceMatrix)
    {
        PdfRectangle deviceRect = CommandHelpers.SnapToDevicePixels(deviceMatrix.MapRect(bounds));

        return deviceMatrix.Invert().MapRect(deviceRect);
    }

    // How thick the mark a fill leaves is, in device pixels. A fill that is not one rectangle covers
    // less than its bounds, and what it leaves out can be thinner than a pixel — the frame a producer
    // drew as a fill rather than a stroke — so it has no thickness of its own, and coverage is what
    // keeps those members on the page.
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
