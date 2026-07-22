using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Converters;
using PdfPixel.Geometry;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.StrokeOutlineLab;

/// <summary>
/// For each shape and cap/join/dash combination, renders three panels side by side: Skia's
/// <see cref="SKPaint.GetFillPath(SKPath)"/> ground truth alone, our own <see cref="PdfStrokeOutlineBuilder"/>
/// outline alone, and both overlaid (Skia in red, ours in blue) so any mismatch is visible as unblended color.
/// </summary>
internal static class Program
{
    private const int PanelWidth = 200;
    private const int PanelHeight = 170;
    private const int PanelGap = 10;
    private const int LabelHeight = 22;
    private const int GroupGap = 14;
    private const float RenderScale = 3f;

    private static void Main(string[] args)
    {
        args = new[] { "--perf" }; 
        if (args.Length > 0 && args[0] == "--diff")
        {
            RunPixelDiff();
            return;
        }

        if (args.Length > 1 && args[0] == "--tile")
        {
            RenderSingleTile(args[1], args.Length > 2 ? args[2] : Path.Combine(AppContext.BaseDirectory, "tile.png"));
            return;
        }

        if (args.Length > 0 && args[0] == "--perf")
        {
            RunFontPerf();
            return;
        }

        string outputPath = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "stroke-outline-comparison.png");

        List<Tile> tiles = BuildTiles();

        int groupWidth = (PanelWidth * 4) + (PanelGap * 3);
        int groupHeight = LabelHeight + PanelHeight;
        int totalHeight = tiles.Count * (groupHeight + GroupGap);

        using SKBitmap bitmap = new((int)(groupWidth * RenderScale), (int)(totalHeight * RenderScale));
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);
        canvas.Scale(RenderScale);

        using SKFont labelFont = new(SKTypeface.Default, 14f);
        using SKFont panelFont = new(SKTypeface.Default, 12f);
        using SKPaint labelPaint = new() { Color = SKColors.Black, IsAntialias = true };
        using SKPaint panelLabelPaint = new() { Color = new SKColor(90, 90, 90), IsAntialias = true };
        using SKPaint borderPaint = new() { Color = new SKColor(210, 210, 210), Style = SKPaintStyle.Stroke, StrokeWidth = 1f };
        using SKPaint referencePaint = new() { Color = new SKColor(0, 0, 0, 90), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };
        using SKPaint skiaFillPaint = new() { Color = new SKColor(220, 30, 30, 190), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint oursFillPaint = new() { Color = new SKColor(30, 60, 220, 190), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint skiaOverlayPaint = new() { Color = new SKColor(220, 30, 30, 130), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint oursOverlayPaint = new() { Color = new SKColor(30, 60, 220, 130), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint oursTranslucentPaint = new() { Color = new SKColor(30, 60, 220, 110), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint checkerLightPaint = new() { Color = new SKColor(255, 255, 255), Style = SKPaintStyle.Fill };
        using SKPaint checkerDarkPaint = new() { Color = new SKColor(225, 225, 225), Style = SKPaintStyle.Fill };

        for (int i = 0; i < tiles.Count; i++)
        {
            Tile tile = tiles[i];
            float groupTop = i * (groupHeight + GroupGap);

            canvas.DrawText(tile.Label, 4, groupTop + 16, labelFont, labelPaint);

            PdfPaint strokePaint = new PdfPaint(tile.Style);
            using SKPaint skiaStrokePaint = strokePaint.ToSkiaPaint();
            using SKPath skiaSource = tile.Path.ToSkPath();
            using SKPath skiaOutline = skiaStrokePaint.GetFillPath(skiaSource) ?? new SKPath(skiaSource);

            PdfPath ourOutline = PdfStrokeOutlineBuilder.BuildOutline(tile.Path, tile.Style);
            using SKPath oursSkPath = ourOutline.ToSkPath();

            float panelTop = groupTop + LabelHeight;

            DrawPanel(canvas, 0, panelTop, "Skia", panelFont, panelLabelPaint, borderPaint, drawCanvas =>
            {
                drawCanvas.DrawPath(skiaOutline, skiaFillPaint);
                drawCanvas.DrawPath(skiaSource, referencePaint);
            });

            DrawPanel(canvas, PanelWidth + PanelGap, panelTop, "Ours", panelFont, panelLabelPaint, borderPaint, drawCanvas =>
            {
                drawCanvas.DrawPath(oursSkPath, oursFillPaint);
                drawCanvas.DrawPath(skiaSource, referencePaint);
            });

            DrawPanel(canvas, (PanelWidth + PanelGap) * 2, panelTop, "Overlay (Skia=red, Ours=blue, match=purple)", panelFont, panelLabelPaint, borderPaint, drawCanvas =>
            {
                drawCanvas.DrawPath(skiaOutline, skiaOverlayPaint);
                drawCanvas.DrawPath(oursSkPath, oursOverlayPaint);
                drawCanvas.DrawPath(skiaSource, referencePaint);
            });

            DrawPanel(canvas, (PanelWidth + PanelGap) * 3, panelTop, "Ours, translucent on checker", panelFont, panelLabelPaint, borderPaint, drawCanvas =>
            {
                DrawCheckerboard(drawCanvas, checkerLightPaint, checkerDarkPaint);
                drawCanvas.DrawPath(oursSkPath, oursTranslucentPaint);
            });
        }

        Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? ".");
        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);

        Console.WriteLine($"Wrote {tiles.Count} groups to {outputPath}");
    }

    private static void RunFontPerf()
    {
        const string SampleText = "The quick brown fox jumps over the lazy dog 0123456789 &@%!?";
        const float FontSize = 200f;
        const int WarmupRounds = 10;
        const int TimedRounds = 1500;

        using SKTypeface typeface = SKTypeface.Default;
        using SKFont font = new(typeface, FontSize);

        ushort[] glyphIds = font.GetGlyphs(SampleText);

        List<SKPath> glyphPaths = [];
        List<PdfPath> pdfPaths = [];
        foreach (ushort glyphId in glyphIds)
        {
            if (glyphId == 0)
            {
                continue;
            }

            SKPath glyphPath = font.GetGlyphPath(glyphId);
            if (glyphPath == null || glyphPath.IsEmpty)
            {
                glyphPath?.Dispose();
                continue;
            }

            glyphPaths.Add(glyphPath);
            pdfPaths.Add(glyphPath.ToPdfPath());
        }

        PdfStrokeStyle style = new(6f, PdfStrokeCap.Round, PdfStrokeJoin.Round, 10f, null, 0f);
        PdfPaint strokePaint = new PdfPaint(style);
        using SKPaint skiaStrokePaint = strokePaint.ToSkiaPaint();

        Console.WriteLine($"Font: {typeface.FamilyName}, characters: {SampleText.Length}, glyphs with outlines: {glyphPaths.Count}");

        for (int round = 0; round < WarmupRounds; round++)
        {
            RunSkiaOutlinePass(glyphPaths, skiaStrokePaint);
            RunOursOutlinePass(pdfPaths, style);
        }

        Stopwatch skiaStopwatch = Stopwatch.StartNew();
        for (int round = 0; round < TimedRounds; round++)
        {
            RunSkiaOutlinePass(glyphPaths, skiaStrokePaint);
        }

        skiaStopwatch.Stop();

        Stopwatch oursStopwatch = Stopwatch.StartNew();
        for (int round = 0; round < TimedRounds; round++)
        {
            RunOursOutlinePass(pdfPaths, style);
        }

        oursStopwatch.Stop();

        long allocBefore = GC.GetAllocatedBytesForCurrentThread();
        RunOursOutlinePass(pdfPaths, style);
        long allocPerRound = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        Console.WriteLine($"Ours allocated: {allocPerRound,8} bytes/round ({(double)allocPerRound / pdfPaths.Count,6:F0} bytes/glyph)");

        long totalCalls = (long)glyphPaths.Count * TimedRounds;
        double skiaMicrosPerGlyph = skiaStopwatch.Elapsed.TotalMicroseconds / totalCalls;
        double oursMicrosPerGlyph = oursStopwatch.Elapsed.TotalMicroseconds / totalCalls;

        Console.WriteLine($"Skia GetFillPath:  {skiaStopwatch.ElapsedMilliseconds,6} ms total ({skiaMicrosPerGlyph,8:F2} us/glyph)");
        Console.WriteLine($"Ours BuildOutline: {oursStopwatch.ElapsedMilliseconds,6} ms total ({oursMicrosPerGlyph,8:F2} us/glyph)");
        Console.WriteLine($"Ratio (Ours / Skia): {oursStopwatch.Elapsed.TotalMilliseconds / skiaStopwatch.Elapsed.TotalMilliseconds:F2}x");

        foreach (SKPath glyphPath in glyphPaths)
        {
            glyphPath.Dispose();
        }
    }

    private static void RunSkiaOutlinePass(List<SKPath> glyphPaths, SKPaint skiaStrokePaint)
    {
        foreach (SKPath glyphPath in glyphPaths)
        {
            using SKPath outline = skiaStrokePaint.GetFillPath(glyphPath) ?? new SKPath(glyphPath);
        }
    }

    private static void RunOursOutlinePass(List<PdfPath> pdfPaths, PdfStrokeStyle style)
    {
        foreach (PdfPath pdfPath in pdfPaths)
        {
            PdfPath outline = PdfStrokeOutlineBuilder.BuildOutline(pdfPath, style);
        }
    }

    private static void RunPixelDiff()
    {
        const int BitmapSize = 400;
        const float Margin = 12f;

        List<Tile> tiles = BuildTiles();
        List<(string Label, int MismatchedPixels)> results = [];

        foreach (Tile tile in tiles)
        {
            PdfPaint strokePaint = new PdfPaint(tile.Style);
            using SKPaint skiaStrokePaint = strokePaint.ToSkiaPaint();
            using SKPath skiaSource = tile.Path.ToSkPath();
            using SKPath skiaOutline = skiaStrokePaint.GetFillPath(skiaSource) ?? new SKPath(skiaSource);

            PdfPath ourOutline = PdfStrokeOutlineBuilder.BuildOutline(tile.Path, tile.Style);
            using SKPath oursSkPath = ourOutline.ToSkPath();

            SKRect bounds = skiaOutline.Bounds;
            bounds.Union(oursSkPath.Bounds);
            bounds.Inflate(Margin, Margin);

            float scale = Math.Min(BitmapSize / bounds.Width, BitmapSize / bounds.Height);

            using SKBitmap skiaBitmap = RasterizeMask(skiaOutline, bounds, scale, BitmapSize);
            using SKBitmap oursBitmap = RasterizeMask(oursSkPath, bounds, scale, BitmapSize);

            int mismatched = 0;
            for (int y = 0; y < BitmapSize; y++)
            {
                for (int x = 0; x < BitmapSize; x++)
                {
                    bool skiaFilled = skiaBitmap.GetPixel(x, y).Alpha > 127;
                    bool oursFilled = oursBitmap.GetPixel(x, y).Alpha > 127;
                    if (skiaFilled != oursFilled)
                    {
                        mismatched++;
                    }
                }
            }

            results.Add((tile.Label, mismatched));
        }

        foreach ((string label, int mismatchedPixels) in results.OrderByDescending(entry => entry.MismatchedPixels))
        {
            Console.WriteLine($"{mismatchedPixels,6}  {label}");
        }
    }

    private static void RenderSingleTile(string labelSubstring, string outputPath)
    {
        Tile tile = BuildTiles().First(candidate => candidate.Label.Contains(labelSubstring, StringComparison.OrdinalIgnoreCase));

        PdfPaint strokePaint = new PdfPaint(tile.Style);
        using SKPaint skiaStrokePaint = strokePaint.ToSkiaPaint();
        using SKPath skiaSource = tile.Path.ToSkPath();
        using SKPath skiaOutline = skiaStrokePaint.GetFillPath(skiaSource) ?? new SKPath(skiaSource);

        PdfPath ourOutline = PdfStrokeOutlineBuilder.BuildOutline(tile.Path, tile.Style);
        using SKPath oursSkPath = ourOutline.ToSkPath();

        SKRect bounds = skiaOutline.Bounds;
        bounds.Union(oursSkPath.Bounds);
        bounds.Inflate(20f, 20f);

        const int PanelSize = 900;
        float scale = Math.Min(PanelSize / bounds.Width, PanelSize / bounds.Height);

        using SKBitmap bitmap = new(PanelSize * 3 + 20, PanelSize);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.White);

        using SKPaint skiaFillPaint = new() { Color = new SKColor(220, 30, 30, 190), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint oursFillPaint = new() { Color = new SKColor(30, 60, 220, 190), Style = SKPaintStyle.Fill, IsAntialias = true };
        using SKPaint referencePaint = new() { Color = new SKColor(0, 0, 0, 120), Style = SKPaintStyle.Stroke, StrokeWidth = 1f, IsAntialias = true };

        void DrawInto(float left, Action<SKCanvas> draw)
        {
            canvas.Save();
            canvas.Translate(left, 0);
            canvas.Translate(-bounds.Left * scale, -bounds.Top * scale);
            canvas.Scale(scale);
            draw(canvas);
            canvas.Restore();
        }

        DrawInto(0, drawCanvas =>
        {
            drawCanvas.DrawPath(skiaOutline, skiaFillPaint);
            drawCanvas.DrawPath(skiaSource, referencePaint);
        });
        DrawInto(PanelSize + 10, drawCanvas =>
        {
            drawCanvas.DrawPath(oursSkPath, oursFillPaint);
            drawCanvas.DrawPath(skiaSource, referencePaint);
        });
        DrawInto((PanelSize + 10) * 2, drawCanvas =>
        {
            drawCanvas.DrawPath(skiaOutline, skiaFillPaint);
            drawCanvas.DrawPath(oursSkPath, oursFillPaint);
            drawCanvas.DrawPath(skiaSource, referencePaint);
        });

        using SKImage image = SKImage.FromBitmap(bitmap);
        using SKData data = image.Encode(SKEncodedImageFormat.Png, 100);
        using FileStream stream = File.OpenWrite(outputPath);
        data.SaveTo(stream);
        Console.WriteLine($"Wrote {outputPath}");
    }

    private static SKBitmap RasterizeMask(SKPath path, SKRect bounds, float scale, int bitmapSize)
    {
        SKBitmap bitmap = new(bitmapSize, bitmapSize);
        using SKCanvas canvas = new(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(-bounds.Left * scale, -bounds.Top * scale);
        canvas.Scale(scale);

        using SKPaint paint = new() { Color = SKColors.Black, Style = SKPaintStyle.Fill, IsAntialias = false };
        canvas.DrawPath(path, paint);
        return bitmap;
    }

    private static void DrawPanel(
        SKCanvas canvas,
        float left,
        float top,
        string panelLabel,
        SKFont panelFont,
        SKPaint panelLabelPaint,
        SKPaint borderPaint,
        Action<SKCanvas> draw)
    {
        canvas.DrawText(panelLabel, left + 4, top + 12, panelFont, panelLabelPaint);
        canvas.DrawRect(left, top + 16, PanelWidth, PanelHeight - 16, borderPaint);

        canvas.Save();
        canvas.ClipRect(new SKRect(left, top + 16, left + PanelWidth, top + PanelHeight));
        canvas.Translate(left, top + 16);
        draw(canvas);
        canvas.Restore();
    }

    private static void DrawCheckerboard(SKCanvas canvas, SKPaint lightPaint, SKPaint darkPaint)
    {
        const int CellSize = 10;

        const int ContentHeight = PanelHeight - 16;

        canvas.DrawRect(new SKRect(0, 0, PanelWidth, ContentHeight), lightPaint);
        for (int row = 0; row * CellSize < ContentHeight; row++)
        {
            for (int column = 0; column * CellSize < PanelWidth; column++)
            {
                if ((row + column) % 2 == 0)
                {
                    continue;
                }

                float left = column * CellSize;
                float top = row * CellSize;
                canvas.DrawRect(new SKRect(left, top, left + CellSize, top + CellSize), darkPaint);
            }
        }
    }

    private sealed record Tile(string Label, PdfPath Path, PdfStrokeStyle Style);

    private static List<Tile> BuildTiles()
    {
        List<Tile> tiles = [];

        PdfStrokeCap[] caps = [PdfStrokeCap.Butt, PdfStrokeCap.Round, PdfStrokeCap.Square];
        PdfStrokeJoin[] joins = [PdfStrokeJoin.Miter, PdfStrokeJoin.Round, PdfStrokeJoin.Bevel];
        float[] dashPattern = [14f, 8f];
        const float LineWidth = 16f;

        // Line: only caps matter.
        foreach (PdfStrokeCap cap in caps)
        {
            foreach (bool dashed in new[] { false, true })
            {
                PdfPath path = BuildLine();
                PdfStrokeStyle style = new(LineWidth, cap, PdfStrokeJoin.Miter, 10f, dashed ? dashPattern : null, 0f);
                tiles.Add(new Tile($"Line — Cap: {cap}, Dashed: {dashed}", path, style));
            }
        }

        // Elbow polyline: both caps and joins are visible.
        foreach (PdfStrokeJoin join in joins)
        {
            foreach (PdfStrokeCap cap in caps)
            {
                foreach (bool dashed in new[] { false, true })
                {
                    PdfPath path = BuildElbow();
                    PdfStrokeStyle style = new(LineWidth, cap, join, 10f, dashed ? dashPattern : null, 0f);
                    tiles.Add(new Tile($"Elbow — Join: {join}, Cap: {cap}, Dashed: {dashed}", path, style));
                }
            }
        }

        // Closed rectangle: joins at 4 sharp corners; dashed rectangle also exercises caps on the dash breaks.
        foreach (PdfStrokeJoin join in joins)
        {
            foreach (bool dashed in new[] { false, true })
            {
                PdfPath path = BuildRect();
                PdfStrokeStyle style = new(LineWidth, PdfStrokeCap.Round, join, 10f, dashed ? dashPattern : null, 0f);
                tiles.Add(new Tile($"Rectangle — Join: {join}, Dashed: {dashed}", path, style));
            }
        }

        // Open cubic curve: caps only, but exercises the curve-offset approximation directly.
        foreach (PdfStrokeCap cap in caps)
        {
            foreach (bool dashed in new[] { false, true })
            {
                PdfPath path = BuildCurve();
                PdfStrokeStyle style = new(LineWidth, cap, PdfStrokeJoin.Round, 10f, dashed ? dashPattern : null, 0f);
                tiles.Add(new Tile($"Curve — Cap: {cap}, Dashed: {dashed}", path, style));
            }
        }

        // Closed oval: the 4 cubic-to-cubic seams are tangent-continuous, so joins should add no visible bump.
        foreach (PdfStrokeJoin join in joins)
        {
            PdfPath path = BuildOval();
            PdfStrokeStyle style = new(LineWidth, PdfStrokeCap.Butt, join, 10f, null, 0f);
            tiles.Add(new Tile($"Oval — Join: {join}", path, style));
        }

        {
            PdfPath path = BuildOval();
            PdfStrokeStyle style = new(LineWidth, PdfStrokeCap.Round, PdfStrokeJoin.Round, 10f, dashPattern, 0f);
            tiles.Add(new Tile("Oval — Dashed", path, style));
        }

        {
            // Isolated repro: exact same tangent directions as the real elbow's vertex (80,-120) and
            // (80,120) normalized, just much shorter legs (12 and 1.7 units) — holds the turn angle fixed
            // while varying only segment length, built directly with no dashing at all.
            (float x, float y) direction1 = Normalize(80, -120);
            (float x, float y) direction2 = Normalize(80, 120);
            PdfPoint vertex = new(100, 20);
            PdfPoint legStart = new(vertex.X - (direction1.x * 12f), vertex.Y - (direction1.y * 12f));
            PdfPoint legEnd = new(vertex.X + (direction2.x * 1.7f), vertex.Y + (direction2.y * 1.7f));

            PdfPathBuilder shortElbowBuilder = new();
            shortElbowBuilder.MoveTo(legStart);
            shortElbowBuilder.LineTo(vertex);
            shortElbowBuilder.LineTo(legEnd);
            PdfPath shortElbow = shortElbowBuilder.ToPath();
            PdfStrokeStyle shortStyle = new(LineWidth, PdfStrokeCap.Butt, PdfStrokeJoin.Round, 10f, null, 0f);
            tiles.Add(new Tile("Isolated short-leg repro (no dash)", shortElbow, shortStyle));
        }

        // Pentagram: a single closed subpath that self-intersects five times. Every join sits at a sharp
        // reentrant-looking vertex, and the crossings stress whether overlapping outline geometry keeps a
        // consistent winding instead of canceling out into unwanted holes.
        foreach (PdfStrokeJoin join in joins)
        {
            foreach (bool dashed in new[] { false, true })
            {
                PdfPath path = BuildStar();
                PdfStrokeStyle style = new(10f, PdfStrokeCap.Round, join, 10f, dashed ? dashPattern : null, 0f);
                tiles.Add(new Tile($"Star (pentagram) — Join: {join}, Dashed: {dashed}", path, style));
            }
        }

        // Crossing X: two unrelated open subpaths whose stroked outlines overlap only at the midpoint,
        // unlike the pentagram where the same subpath crosses itself.
        foreach (PdfStrokeCap cap in caps)
        {
            foreach (bool dashed in new[] { false, true })
            {
                PdfPath path = BuildCrossingX();
                PdfStrokeStyle style = new(LineWidth, cap, PdfStrokeJoin.Miter, 10f, dashed ? dashPattern : null, 0f);
                tiles.Add(new Tile($"Crossing X — Cap: {cap}, Dashed: {dashed}", path, style));
            }
        }

        // Round-cap dash with a wide gap: the gap (30) is much larger than the line width (6), so each
        // round cap renders as a clearly isolated bump instead of overlapping its neighbor.
        {
            PdfPath path = BuildElbow();
            PdfStrokeStyle style = new(6f, PdfStrokeCap.Round, PdfStrokeJoin.Round, 10f, [10f, 30f], 0f);
            tiles.Add(new Tile("Elbow — Round cap, wide dash gap", path, style));
        }

        // Font glyph outlines: real letterforms mix tight cusps, near-tangent curve joins, and (for '&')
        // self-overlapping loops, none of which are hand-built shapes above exercise.
        foreach (char glyphChar in new[] { '&', 'S', '@' })
        {
            {
                PdfPath path = BuildGlyphOutline(glyphChar);
                PdfStrokeStyle style = new(6f, PdfStrokeCap.Butt, PdfStrokeJoin.Miter, 10f, null, 0f);
                tiles.Add(new Tile($"Glyph '{glyphChar}' — Normal", path, style));
            }

            foreach (PdfStrokeJoin join in joins)
            {
                PdfPath path = BuildGlyphOutline(glyphChar);
                PdfStrokeStyle style = new(6f, PdfStrokeCap.Round, join, 10f, null, 0f);
                tiles.Add(new Tile($"Glyph '{glyphChar}' — Join: {join}", path, style));
            }

            {
                PdfPath path = BuildGlyphOutline(glyphChar);
                PdfStrokeStyle style = new(6f, PdfStrokeCap.Round, PdfStrokeJoin.Round, 10f, [8f, 6f], 0f);
                tiles.Add(new Tile($"Glyph '{glyphChar}' — Dashed", path, style));
            }
        }

        return tiles;
    }

    private static (float x, float y) Normalize(float x, float y)
    {
        float length = MathF.Sqrt((x * x) + (y * y));
        return (x / length, y / length);
    }

    private static PdfPath BuildLine()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(20, 80);
        builder.LineTo(180, 80);
        return builder.ToPath();
    }

    private static PdfPath BuildElbow()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(20, 140);
        builder.LineTo(100, 20);
        builder.LineTo(180, 140);
        return builder.ToPath();
    }

    private static PdfPath BuildRect()
    {
        PdfPathBuilder builder = new();
        builder.AddRect(new PdfRectangle(30, 30, 170, 140));
        return builder.ToPath();
    }

    private static PdfPath BuildCurve()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(20, 130);
        builder.CubicTo(60, 10, 140, 10, 180, 130);
        return builder.ToPath();
    }

    private static PdfPath BuildOval()
    {
        PdfPathBuilder builder = new();
        builder.AddOval(new PdfRectangle(30, 20, 170, 150));
        return builder.ToPath();
    }

    private static PdfPath BuildCrossingX()
    {
        PdfPathBuilder builder = new();
        builder.MoveTo(30, 30);
        builder.LineTo(170, 140);
        builder.MoveTo(170, 30);
        builder.LineTo(30, 140);
        return builder.ToPath();
    }

    private static PdfPath BuildStar()
    {
        const float CenterX = 100f;
        const float CenterY = 85f;
        const float OuterRadius = 75f;
        const int PointCount = 5;

        var outerPoints = new PdfPoint[PointCount];
        for (int i = 0; i < PointCount; i++)
        {
            float angle = (-MathF.PI / 2f) + (i * 2f * MathF.PI / PointCount);
            outerPoints[i] = new PdfPoint(CenterX + (OuterRadius * MathF.Cos(angle)), CenterY + (OuterRadius * MathF.Sin(angle)));
        }

        PdfPathBuilder builder = new();
        builder.MoveTo(outerPoints[0]);
        for (int step = 1; step <= PointCount; step++)
        {
            builder.LineTo(outerPoints[(step * 2) % PointCount]);
        }

        builder.Close();
        return builder.ToPath();
    }

    private static PdfPath BuildGlyphOutline(char character)
    {
        using SKTypeface typeface = SKTypeface.Default;
        using SKFont font = new(typeface, 160f);

        ushort[] glyphIds = font.GetGlyphs(character.ToString());
        using SKPath glyphPath = font.GetGlyphPath(glyphIds[0]);

        SKRect bounds = glyphPath.Bounds;
        const float TargetWidth = 140f;
        const float TargetHeight = 130f;
        float scale = Math.Min(TargetWidth / bounds.Width, TargetHeight / bounds.Height);

        SKMatrix transform = SKMatrix.CreateScaleTranslation(
            scale,
            scale,
            30f - (bounds.Left * scale),
            20f - (bounds.Top * scale));
        glyphPath.Transform(transform);

        return glyphPath.ToPdfPath();
    }
}
