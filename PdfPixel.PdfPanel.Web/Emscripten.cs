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

	// [UnmanagedCallersOnly] cannot be generic, so TCS completion is baked into the
	// wrapper Action — the callback is a simple trampoline for both overloads below.
	[System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
	[UnmanagedCallersOnly]
	private static void AsyncMainThreadCallback(nint ptr)
	{
		// Exceptions must never escape an [UnmanagedCallersOnly] boundary; doing so
		// is undefined behaviour in WASM and causes a runtime trap/memory error.
		GCHandle handle = default;
		try
		{
			handle = GCHandle.FromIntPtr(ptr);
			var action = (Action)handle.Target!;
			// Run action() before freeing the handle so the wrapper lambda remains
			// GC-rooted through the handle for the entire duration of the call,
			// not just via the local variable (which may not be visible on the
			// native WASM call stack during a GC pause).
			action();
		}
		catch
		{
			// Swallow: either the handle was invalid (programming error) or
			// the wrapper lambda threw unexpectedly. The TCS will not be set,
			// which is preferable to crashing the WASM process.
		}
		finally
		{
			if (handle.IsAllocated)
			{
				handle.Free();
			}
		}
	}

	// Runs action() on the browser main thread without blocking the calling thread.
	// Returns a Task that completes (on the calling thread's scheduler) once action() returns.
	// Use instead of SyncMainThread to avoid potential deadlocks when the main thread
	// may itself be waiting on something the worker thread needs to service.
	internal static unsafe Task RunOnMainThreadAsync(Action action)
	{
		var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		Action wrapper = () =>
		{
			try { action(); tcs.SetResult(); }
			catch (Exception ex) { tcs.SetException(ex); }
		};
		var handle = GCHandle.Alloc(wrapper);
		AsyncRunInMainRuntimeThread(&AsyncMainThreadCallback, GCHandle.ToIntPtr(handle));
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
        AsyncRunInMainRuntimeThread(&AsyncMainThreadCallback, GCHandle.ToIntPtr(handle));
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
		AsyncRunInMainRuntimeThread(&AsyncMainThreadCallback, GCHandle.ToIntPtr(handle));
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
		AsyncRunInMainRuntimeThread(&AsyncMainThreadCallback, GCHandle.ToIntPtr(handle));
		return tcs.Task;
	}
}
