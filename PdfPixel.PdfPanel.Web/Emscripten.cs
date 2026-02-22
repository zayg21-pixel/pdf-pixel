using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace WebGL.Sample;

internal static class Emscripten
{
	// Continuous rendering loop (calls automatically every frame)
	[DllImport("emscripten", EntryPoint = "emscripten_request_animation_frame_loop")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	internal static extern unsafe void RequestAnimationFrameLoop(void* f, nint userDataPtr);

	// Single frame rendering (must be called again for next frame)
	[DllImport("emscripten", EntryPoint = "emscripten_request_animation_frame")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	internal static extern unsafe long RequestAnimationFrame(void* f, nint userDataPtr);

	// Cancel an animation frame request
	[DllImport("emscripten", EntryPoint = "emscripten_cancel_animation_frame")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	internal static extern void CancelAnimationFrame(long requestId);

	// Dispatch a void() function to the browser main thread synchronously.
	// Blocks the calling thread until the main thread has finished executing func.
	[DllImport("emscripten", EntryPoint = "dotnet_sync_main_thread")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	internal static extern unsafe void SyncMainThread(delegate* unmanaged<void> func);

	// Same, but forwards a single nint argument to the callback.
	// On WASM32 nint is 32-bit, matching EM_FUNC_SIG_VI (void(int)).
	[DllImport("emscripten", EntryPoint = "dotnet_sync_main_thread_arg")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	internal static extern unsafe void SyncMainThread(delegate* unmanaged<nint, void> func, nint arg);

	// Non-blocking variant: posts func(arg) to the main thread and returns immediately.
	[DllImport("emscripten", EntryPoint = "dotnet_async_run_in_main_runtime_thread")]
	[DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
	private static extern unsafe void AsyncRunInMainRuntimeThread(delegate* unmanaged<nint, void> func, nint arg);

	// Cached WASM function-table index for AsyncMainThreadCallback, resolved once during
	// type initialization to guarantee a stable index across threads.
	// Repeated '&AsyncMainThreadCallback' evaluation in multi-threaded WASM can race
	// with the runtime's function-table growth, yielding a stale or out-of-bounds index.
	private static readonly nint AsyncMainThreadCallbackPtr;

	static unsafe Emscripten()
	{
		AsyncMainThreadCallbackPtr = (nint)(delegate* unmanaged<nint, void>)&AsyncMainThreadCallback;
	}

	// Returns the WebGL context handle current on the calling thread (0 if none).
    [DllImport("emscripten", EntryPoint = "dotnet_webgl_get_current_context")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int WebGlGetCurrentContext();

    // Creates a WebGL context on the specified canvas with the given attributes.
    // renderViaOffscreenBackBuffer=1 is set internally — required for OFFSCREEN_FRAMEBUFFER
    // worker rendering. Returns the handle (> 0) on success, negative EMSCRIPTEN_RESULT on error.
    [DllImport("emscripten", EntryPoint = "dotnet_webgl_create_context")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int WebGlCreateContext(string canvasId, int alpha, int depth, int stencil, int antialias, int majorVersion);

    // Makes the given WebGL context current on the calling thread.
    // With OFFSCREEN_FRAMEBUFFER=1, safe to call from any pthread — GL calls are proxied to the main browser thread.
    [DllImport("emscripten", EntryPoint = "dotnet_webgl_make_context_current")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern int WebGlMakeContextCurrent(int context);

    // Sets the canvas size. Must be called from the main browser thread.
    // Setting canvas dimensions clears the canvas, so call this just before recreating the render target.
    [DllImport("emscripten", EntryPoint = "dotnet_set_canvas_size")]
    [DefaultDllImportSearchPaths(DllImportSearchPath.SafeDirectories)]
    internal static extern void SetCanvasSize(string canvasId, int width, int height);

    // [UnmanagedCallersOnly] cannot be generic, so TCS completion is baked into the
    // wrapper Action — the callback is a simple trampoline for both overloads below.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	[UnmanagedCallersOnly]
	private static void AsyncMainThreadCallback(nint ptr)
	{
		if (ptr == 0)
		{
			Console.Error.WriteLine("Emscripten.AsyncMainThreadCallback called with null pointer.");
			return;
		}

		GCHandle handle = default;
		try
		{
			handle = GCHandle.FromIntPtr(ptr);
			var action = (Action)handle.Target!;
			action();
		}
		catch (Exception ex)
		{
			Console.Error.WriteLine($"Emscripten.AsyncMainThreadCallback failed: {ex}");
		}
		finally
		{
			if (handle.IsAllocated)
			{
				handle.Free();
			}
		}
	}

	internal static unsafe Task RunOnMainThreadAsync(Action action)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action wrapper = () =>
		{
			try { action(); tcs.SetResult(); }
			catch (Exception ex) { tcs.SetException(ex); }
		};
		var handle = GCHandle.Alloc(wrapper);
		AsyncRunInMainRuntimeThread((delegate* unmanaged<nint, void>)AsyncMainThreadCallbackPtr, GCHandle.ToIntPtr(handle));
		return tcs.Task;
	}

	internal static unsafe Task<Out> RunOnMainThreadAsync<In, Out>(Func<In, Out> func, In parameter)
    {
        var tcs = new TaskCompletionSource<Out>(TaskCreationOptions.RunContinuationsAsynchronously);
        Action wrapper = () =>
        {
            try { tcs.SetResult(func(parameter)); }
            catch (Exception ex) { tcs.SetException(ex); }
        };
		var handle = GCHandle.Alloc(wrapper);
		AsyncRunInMainRuntimeThread((delegate* unmanaged<nint, void>)AsyncMainThreadCallbackPtr, GCHandle.ToIntPtr(handle));
		return tcs.Task;
	}

	// Same, but runs func() on the main thread and returns its result as Task<T>.
    internal static unsafe Task<T> RunOnMainThreadAsync<T>(Func<T> func)
	{
		var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
		Action wrapper = () =>
		{
			try { tcs.SetResult(func()); }
			catch (Exception ex) { tcs.SetException(ex); }
		};
		var handle = GCHandle.Alloc(wrapper);
		AsyncRunInMainRuntimeThread((delegate* unmanaged<nint, void>)AsyncMainThreadCallbackPtr, GCHandle.ToIntPtr(handle));
		return tcs.Task;
	}

	// Same as RunOnMainThreadAsync(Action), but passes data to the action directly.
	// Avoids a closure allocation when the action only needs a single piece of state.
	internal static unsafe Task RunOnMainThreadAsync<T>(Action<T> action, T data)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action wrapper = () =>
		{
			try { action(data); tcs.SetResult(); }
			catch (Exception ex) { tcs.SetException(ex); }
		};
		var handle = GCHandle.Alloc(wrapper);
		AsyncRunInMainRuntimeThread((delegate* unmanaged<nint, void>)AsyncMainThreadCallbackPtr, GCHandle.ToIntPtr(handle));
		return tcs.Task;
	}
}
