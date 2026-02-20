#include "emscripten.h"
#include <emscripten/threading.h>

// Run func() on the browser main thread and block until it returns.
// Safe to call from any thread, including the main thread itself.
void dotnet_sync_main_thread(void (*func)(void)) {
    if (emscripten_is_main_browser_thread()) {
        func();
    } else {
        emscripten_sync_run_in_main_runtime_thread(EM_FUNC_SIG_V, func);
    }
}

// Pointer-sized integer type and matching EM_FUNC_SIG for emscripten threading:
//   MEMORY64=0 (wasm32): void* == int32_t  → EM_FUNC_SIG_VI
//   MEMORY64=1 (wasm64): void* == int64_t  → EM_FUNC_SIG_VJ
//   MEMORY64=2 (wasm64 lowered to wasm32 by Binaryen): not clearly defined yet.
#ifdef __wasm64__
#  define EM_FUNC_SIG_VPTR EM_FUNC_SIG_VJ
   typedef int64_t em_ptr_int_t;
#else
#  define EM_FUNC_SIG_VPTR EM_FUNC_SIG_VI
   typedef int32_t em_ptr_int_t;
#endif

// Same as dotnet_sync_main_thread, but forwards a single pointer-sized argument.
// On the C# side use nint / delegate* unmanaged<nint, void> — nint matches em_ptr_int_t
// on both wasm32 and wasm64.
void dotnet_sync_main_thread_arg(void (*func)(em_ptr_int_t), em_ptr_int_t arg) {
    if (emscripten_is_main_browser_thread()) {
        func(arg);
    } else {
        emscripten_sync_run_in_main_runtime_thread(EM_FUNC_SIG_VPTR, func, arg);
    }
}

// Non-blocking variant: posts func(arg) to the main thread and returns immediately.
// Used to implement C# async/await dispatch — the worker thread suspends via
// TaskCompletionSource rather than blocking, so neither thread can deadlock on the other.
void dotnet_async_run_in_main_runtime_thread(void (*func)(em_ptr_int_t), em_ptr_int_t arg) {
    if (emscripten_is_main_browser_thread()) {
        func(arg);
    } else {
        emscripten_async_run_in_main_runtime_thread(EM_FUNC_SIG_VPTR, func, arg);
    }
}
