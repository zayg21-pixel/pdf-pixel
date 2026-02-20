using System.Threading.Tasks;

namespace PdfPixel.PdfPanel.Web;

/// <summary>
/// Static factory for creating EGL and Skia rendering resources.
/// </summary>
public static class SkiaInitializer
{
	/// <summary>
	/// Initializes the shared EGL display and configuration.
	/// May be called from any thread.
	/// </summary>
	/// <returns>The shared <see cref="EglGlobalContext"/>.</returns>
	public static EglGlobalContext InitializeDisplay()
	{
		return EglGlobalContext.Initialize();
	}

	/// <summary>
	/// Creates a per-canvas GPU context for the currently targeted canvas.
	/// EGL setup runs on any thread; Skia setup is marshalled to the browser main thread internally.
	/// </summary>
	/// <param name="globalContext">The shared EGL display and configuration.</param>
	/// <returns>A <see cref="CanvasGlContext"/> bound to the active canvas.</returns>
	public static Task<CanvasGlContext> CreateCanvasContextAsync(EglGlobalContext globalContext)
	{
		return CanvasGlContext.CreateAsync(globalContext);
	}
}
