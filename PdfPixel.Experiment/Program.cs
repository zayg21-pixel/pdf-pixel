using System.Diagnostics;
using Silk.NET.Maths;
using Silk.NET.Windowing;
using SkiaSharp;

const int ImageCount = 40_000;

IWindow window = Window.Create(WindowOptions.Default with
{
    Size = new Vector2D<int>(800, 600),
    Title = "SkiaSharp OpenGL Experiment",
    API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(3, 3)),
});

GRContext? grContext = null;
SKSurface? surface = null;
SKPicture? picture = null;

window.Load += () =>
{
    GRGlInterface glInterface = GRGlInterface.Create();
    grContext = GRContext.CreateGl(glInterface);

    Console.WriteLine($"GRContext created: backend = {grContext.Backend}");
    LogGrContextResources("After GRContext creation", grContext);
    LogProcessMemory("After GRContext creation");

    // === EXPERIMENT SELECTOR — comment/uncomment one ===
    //picture = CreatePicture_40kSeparateImages();
    //picture = CreatePicture_OneImageDrawRegions();
    picture = CreatePicture_AtlasShaderRegions();
    // ===================================================

    Console.WriteLine($"Picture.ApproximateBytesUsed = {picture.ApproximateBytesUsed:N0} bytes ({picture.ApproximateBytesUsed / 1024.0 / 1024.0:F2} MB)");
    LogGrContextResources("After picture creation", grContext);
    LogProcessMemory("After picture creation");
};

window.Render += _ =>
{
    if (grContext == null || picture == null)
    {
        return;
    }

    surface?.Dispose();

    GRBackendRenderTarget renderTarget = new(
        window.Size.X,
        window.Size.Y,
        0,
        8,
        new GRGlFramebufferInfo(0, 0x8058)); // GL_RGBA8

    surface = SKSurface.Create(grContext, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);

    if (surface == null)
    {
        Console.WriteLine("Failed to create GPU surface.");
        return;
    }

    SKCanvas canvas = surface.Canvas;
    canvas.Clear(SKColors.White);

    LogGrContextResources("Before DrawPicture", grContext);
    LogProcessMemory("Before DrawPicture");

    Stopwatch stopwatch = Stopwatch.StartNew();
    canvas.DrawPicture(picture);
    canvas.Flush();
    grContext.Flush();
    stopwatch.Stop();

    Console.WriteLine($"DrawPicture took {stopwatch.Elapsed.TotalMilliseconds:F2} ms");

    LogGrContextResources("After DrawPicture + Flush", grContext);
    LogProcessMemory("After DrawPicture + Flush");

    window.Close();
};

window.Closing += () =>
{
    surface?.Dispose();
    picture?.Dispose();
    grContext?.Dispose();
};

window.Run();

// ============================================================
// Experiment 1: 40k separate 1x1 Gray8 images (baseline)
// ============================================================
static SKPicture CreatePicture_40kSeparateImages()
{
    Console.WriteLine("=== Experiment: 40k separate 1x1 Gray8 images ===");
    LogProcessMemory("Before creating images");

    SKImage[] images = new SKImage[ImageCount];

    for (int i = 0; i < ImageCount; i++)
    {
        SKImageInfo imageInfo = new(1, 1, SKColorType.Gray8, SKAlphaType.Opaque);
        using SKSurface imageSurface = SKSurface.Create(imageInfo);
        imageSurface.Canvas.Clear(new SKColor(128, 128, 128));
        images[i] = imageSurface.Snapshot();
    }

    Console.WriteLine($"Created {ImageCount} Gray8 1x1 images.");
    LogProcessMemory("After creating images");

    using SKPictureRecorder recorder = new();
    SKCanvas canvas = recorder.BeginRecording(new SKRect(0, 0, 800, 600));

    float x = 0;
    float y = 0;

    for (int i = 0; i < ImageCount; i++)
    {
        canvas.DrawImage(images[i], x, y, SKSamplingOptions.Default);
        x += 1;
        if (x >= 800)
        {
            x = 0;
            y += 1;
        }
    }

    SKPicture picture = recorder.EndRecording();
    LogProcessMemory("After EndRecording");

    for (int i = 0; i < ImageCount; i++)
    {
        images[i].Dispose();
    }

    LogProcessMemory("After disposing images");
    return picture;
}

// ============================================================
// Experiment 2: One large image, draw 40k regions from it
// ============================================================
static SKPicture CreatePicture_OneImageDrawRegions()
{
    Console.WriteLine("=== Experiment: 1 large image, 40k DrawImage with src regions ===");
    LogProcessMemory("Before creating atlas image");

    int atlasWidth = 200;
    int atlasHeight = 200;
    SKImageInfo atlasInfo = new(atlasWidth, atlasHeight, SKColorType.Gray8, SKAlphaType.Opaque);
    using SKSurface atlasSurface = SKSurface.Create(atlasInfo);
    atlasSurface.Canvas.Clear(new SKColor(128, 128, 128));
    SKImage atlasImage = atlasSurface.Snapshot();

    Console.WriteLine($"Created 1 atlas image ({atlasWidth}x{atlasHeight} Gray8 = {atlasWidth * atlasHeight} pixels).");
    LogProcessMemory("After creating atlas image");

    using SKPictureRecorder recorder = new();
    SKCanvas canvas = recorder.BeginRecording(new SKRect(0, 0, 800, 600));

    float destX = 0;
    float destY = 0;
    int srcX = 0;
    int srcY = 0;

    for (int i = 0; i < ImageCount; i++)
    {
        SKRect source = new(srcX, srcY, srcX + 1, srcY + 1);
        SKRect dest = new(destX, destY, destX + 1, destY + 1);
        canvas.DrawImage(atlasImage, source, dest, SKSamplingOptions.Default);

        srcX++;
        if (srcX >= atlasWidth)
        {
            srcX = 0;
            srcY++;
        }

        destX += 1;
        if (destX >= 800)
        {
            destX = 0;
            destY += 1;
        }
    }

    SKPicture picture = recorder.EndRecording();
    LogProcessMemory("After EndRecording");

    atlasImage.Dispose();
    LogProcessMemory("After disposing atlas image");
    return picture;
}

// ============================================================
// Experiment 3: Atlas image, shader with region via DrawPaint
// Replicates how the PDF pipeline renders: BuildImageShader +
// canvas scale/clip/translate + DrawPaint
// ============================================================
static SKPicture CreatePicture_AtlasShaderRegions()
{
    Console.WriteLine("=== Experiment: Atlas image, shader regions via DrawPaint ===");
    LogProcessMemory("Before creating atlas image");

    int atlasWidth = 200;
    int atlasHeight = 200;
    SKImageInfo atlasInfo = new(atlasWidth, atlasHeight, SKColorType.Gray8, SKAlphaType.Opaque);
    using SKSurface atlasSurface = SKSurface.Create(atlasInfo);

    // Fill each pixel with a different gray value so we can verify correctness
    SKCanvas atlasCanvas = atlasSurface.Canvas;
    for (int py = 0; py < atlasHeight; py++)
    {
        for (int px = 0; px < atlasWidth; px++)
        {
            byte gray = (byte)((px + py * atlasWidth) % 256);
            using SKPaint pixelPaint = new() { Color = new SKColor(gray, gray, gray) };
            atlasCanvas.DrawPoint(px, py, pixelPaint);
        }
    }

    SKImage atlasImage = atlasSurface.Snapshot();
    Console.WriteLine($"Created 1 atlas image ({atlasWidth}x{atlasHeight} Gray8).");
    LogProcessMemory("After creating atlas image");

    using SKPictureRecorder recorder = new();
    SKCanvas canvas = recorder.BeginRecording(new SKRect(0, 0, 800, 600));

    int srcX = 0;
    int srcY = 0;

    // Each "tile" is 1x1 pixel from the atlas, drawn at 1x1 destination
    // using the same pattern as DrawNormalImageTileCommand:
    //   shader = BuildImageShader(image, targetSize, sampling)
    //   canvas.Save / Scale / ClipRect / Translate / DrawPaint / Restore
    int tileWidth = 1;
    int tileHeight = 1;
    int imageWidth = 200;  // simulated "full image" dimensions (tile grid)
    int imageHeight = 200;

    float destX = 0;
    float destY = 0;

    for (int i = 0; i < ImageCount; i++)
    {
        SKRectI sourceRegion = SKRectI.Create(srcX, srcY, tileWidth, tileHeight);

        using SKShader shader = BuildImageShaderWithRegion(
            atlasImage, sourceRegion,
            new SKSizeI(tileWidth, tileHeight),
            SKSamplingOptions.Default);

        using SKPaint paint = new();
        paint.Shader = shader;

        canvas.Save();
        canvas.Translate(destX, destY);
        canvas.ClipRect(new SKRect(0, 0, tileWidth, tileHeight));
        canvas.DrawPaint(paint);
        canvas.Restore();

        srcX++;
        if (srcX >= atlasWidth)
        {
            srcX = 0;
            srcY++;
        }

        destX += 1;
        if (destX >= 800)
        {
            destX = 0;
            destY += 1;
        }
    }

    SKPicture picture = recorder.EndRecording();
    LogProcessMemory("After EndRecording");

    atlasImage.Dispose();
    LogProcessMemory("After disposing atlas image");
    return picture;
}

// ============================================================
// BuildImageShader — original from ImageBlending
// ============================================================
static SKShader BuildImageShader(
    SKImage source,
    SKSizeI targetSize,
    in SKSamplingOptions sampling)
{
    return source.ToShader(
        SKShaderTileMode.Clamp,
        SKShaderTileMode.Clamp,
        sampling,
        SKMatrix.CreateScale((float)targetSize.Width / source.Width, (float)targetSize.Height / source.Height));
}

// ============================================================
// BuildImageShaderWithRegion — takes a source region from atlas
// ============================================================
static SKShader BuildImageShaderWithRegion(
    SKImage atlasImage,
    SKRectI sourceRegion,
    SKSizeI targetSize,
    in SKSamplingOptions sampling)
{
    // Translate so the region's top-left maps to (0,0), then scale region to target size
    SKMatrix matrix = SKMatrix.CreateScale(
        (float)targetSize.Width / sourceRegion.Width,
        (float)targetSize.Height / sourceRegion.Height);
    matrix = matrix.PreConcat(SKMatrix.CreateTranslation(-sourceRegion.Left, -sourceRegion.Top));

    return atlasImage.ToShader(
        SKShaderTileMode.Clamp,
        SKShaderTileMode.Clamp,
        sampling,
        matrix);
}

// ============================================================
// Logging helpers
// ============================================================
static void LogGrContextResources(string label, GRContext? context)
{
    if (context == null)
    {
        return;
    }

    context.GetResourceCacheUsage(out int resourceCount, out long resourceBytes);
    long cacheLimit = context.GetResourceCacheLimit();

    Console.WriteLine($"[GPU Resources] {label}:");
    Console.WriteLine($"  Resources: {resourceCount:N0}");
    Console.WriteLine($"  Resource bytes: {resourceBytes:N0} ({resourceBytes / 1024.0 / 1024.0:F2} MB)");
    Console.WriteLine($"  Cache limit: {cacheLimit:N0} ({cacheLimit / 1024.0 / 1024.0:F2} MB)");
}

static void LogProcessMemory(string label)
{
    Process process = Process.GetCurrentProcess();
    long workingSet = process.WorkingSet64;
    long privateBytes = process.PrivateMemorySize64;
    long gcTotal = GC.GetTotalMemory(false);

    Console.WriteLine($"[Process Memory] {label}:");
    Console.WriteLine($"  Working set: {workingSet:N0} ({workingSet / 1024.0 / 1024.0:F2} MB)");
    Console.WriteLine($"  Private bytes: {privateBytes:N0} ({privateBytes / 1024.0 / 1024.0:F2} MB)");
    Console.WriteLine($"  GC total: {gcTotal:N0} ({gcTotal / 1024.0 / 1024.0:F2} MB)");
}
