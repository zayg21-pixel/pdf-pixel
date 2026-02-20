using PdfPixel.PdfPanel.Wpf.D3D.Interop;
using SkiaSharp;
using System;
using static PdfPixel.PdfPanel.Wpf.D3D.DirectXInterop;

namespace PdfPixel.PdfPanel.Wpf.D3D;

/// <summary>
/// Manages DirectX device initialization for D3D9Ex, D3D11, and D3D12, anchored to
/// the D3D9-visible display adapter used by WPF's compositor.
/// All COM objects are held as raw <see cref="IntPtr"/> handles via <see cref="DirectXHandle"/>.
/// </summary>
public class Direct3DContext : IDisposable
{
    private DirectXHandle _factory;
    private DirectXHandle _adapter;
    private DirectXHandle _d3d12Device;
    private DirectXHandle _d3d11Device;
    private DirectXHandle _d3d9;
    private DirectXHandle _d3d9Device;
    private DirectXHandle _queue;
    private DirectXHandle _d3d11Context;
    private bool _disposed;

    private Direct3DContext() { }

    /// <summary>Creates and fully initializes a new <see cref="Direct3DContext"/>.</summary>
    public static Direct3DContext Create()
    {
        var context = new Direct3DContext();
        context.Initialize();
        return context;
    }

    /// <summary>
    /// Initializes all DirectX devices anchored to the D3D9-visible display adapter.
    /// D3D9Ex only enumerates adapters that are actively driving a monitor, which is
    /// the same adapter WPF's compositor uses. Anchoring D3D11 and D3D12 to the same
    /// adapter is required for D3DImage shared surface compatibility.
    /// </summary>
    private void Initialize()
    {
        ThrowIfFailed(CreateFactory(out var factoryPtr));
        _factory = new DirectXHandle(factoryPtr);

        ThrowIfFailed(CreateD3D9Ex(out var d3d9Ptr));
        if (d3d9Ptr == IntPtr.Zero)
        {
            throw new NotSupportedException("Unable to create Direct3D 9Ex instance.");
        }

        _d3d9 = new DirectXHandle(d3d9Ptr);

        uint d3d9AdapterCount = D3D9GetAdapterCount(d3d9Ptr);
        for (uint d3d9Index = 0; d3d9Index < d3d9AdapterCount; d3d9Index++)
        {
            ThrowIfFailed(D3D9GetAdapterLuid(d3d9Ptr, d3d9Index, out var d3d9Luid));

            for (uint dxgiIndex = 0; ; dxgiIndex++)
            {
                int enumResult = DxgiFactoryEnumAdapters1(factoryPtr, dxgiIndex, out var candidatePtr);
                if (!Succeeded(enumResult))
                {
                    break;
                }

                ThrowIfFailed(DxgiAdapterGetLuid(candidatePtr, out var adapterLuid));
                bool luidMatches = adapterLuid.LowPart == d3d9Luid.LowPart &&
                                   adapterLuid.HighPart == d3d9Luid.HighPart;

                if (!luidMatches)
                {
                    ComRelease(candidatePtr);
                    continue;
                }

                if (!Succeeded(CreateD3D12Device(candidatePtr, FeatureLevel110, out var d3d12DevicePtr)))
                {
                    ComRelease(candidatePtr);
                    break;
                }

                int d3d11Hr = CreateD3D11Device(
                    candidatePtr,
                    D3D11CreateDeviceBgraSupport,
                    [FeatureLevel111, FeatureLevel110],
                    out var d3d11DevicePtr,
                    out var d3d11ContextPtr);

                if (!Succeeded(d3d11Hr))
                {
                    ComRelease(d3d12DevicePtr);
                    ComRelease(candidatePtr);
                    break;
                }

                var presentParams = new D3DPresentParameters
                {
                    Windowed = 1,
                    SwapEffect = D3D9SwapEffectDiscard,
                    DeviceWindow = IntPtr.Zero,
                    PresentationInterval = D3D9PresentIntervalDefault,
                    BackBufferFormat = D3D9FormatUnknown,
                    BackBufferWidth = 1,
                    BackBufferHeight = 1
                };

                int d3d9DeviceHr = D3D9CreateDeviceEx(
                    d3d9Ptr,
                    d3d9Index,
                    D3D9DeviceTypeHal,
                    IntPtr.Zero,
                    D3D9CreateHardwareVertexProcessing | D3D9CreateMultithreaded | D3D9CreateFpuPreserve,
                    ref presentParams,
                    out var d3d9DevicePtr);

                if (!Succeeded(d3d9DeviceHr))
                {
                    ComRelease(d3d11ContextPtr);
                    ComRelease(d3d11DevicePtr);
                    ComRelease(d3d12DevicePtr);
                    ComRelease(candidatePtr);
                    break;
                }

                _adapter = new DirectXHandle(candidatePtr);
                _d3d12Device = new DirectXHandle(d3d12DevicePtr);
                _d3d11Device = new DirectXHandle(d3d11DevicePtr);
                _d3d11Context = new DirectXHandle(d3d11ContextPtr);
                _d3d9Device = new DirectXHandle(d3d9DevicePtr);

                var queueDesc = new D3D12CommandQueueDesc
                {
                    Flags = D3D12CommandQueueFlagNone,
                    Type = D3D12CommandListTypeDirect
                };

                ThrowIfFailed(D3D12DeviceCreateCommandQueue(d3d12DevicePtr, ref queueDesc, out var queuePtr));
                _queue = new DirectXHandle(queuePtr);
                return;
            }
        }

        throw new NotSupportedException("No suitable adapter found that supports D3D9Ex, D3D11, and D3D12.");
    }

    /// <summary>The DXGI adapter pointer.</summary>
    public IntPtr AdapterPointer => _adapter.Ptr;

    /// <summary>The D3D12 device pointer.</summary>
    public IntPtr D3D12DevicePointer => _d3d12Device.Ptr;

    /// <summary>The D3D11 device pointer.</summary>
    public IntPtr D3D11DevicePointer => _d3d11Device.Ptr;

    /// <summary>The D3D11 immediate context pointer.</summary>
    public IntPtr D3D11ContextPointer => _d3d11Context.Ptr;

    /// <summary>The Direct3D 9Ex instance pointer.</summary>
    public IntPtr D3D9Pointer => _d3d9.Ptr;

    /// <summary>The Direct3D 9Ex device pointer.</summary>
    public IntPtr D3D9DevicePointer => _d3d9Device.Ptr;

    /// <summary>The D3D12 command queue pointer.</summary>
    public IntPtr QueuePointer => _queue.Ptr;

    /// <summary>
    /// Creates a <see cref="GRD3DBackendContext"/> for SkiaSharp GPU rendering
    /// using the initialized D3D12 device and command queue.
    /// </summary>
    public GRD3DBackendContext CreateBackendContext()
    {
        return new GRD3DBackendContext
        {
            Adapter = _adapter.Ptr,
            Device = _d3d12Device.Ptr,
            Queue = _queue.Ptr
        };
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _queue?.Dispose();
        _d3d9Device?.Dispose();
        _d3d9?.Dispose();
        _d3d11Context?.Dispose();
        _d3d11Device?.Dispose();
        _d3d12Device?.Dispose();
        _adapter?.Dispose();
        _factory?.Dispose();
    }
}
