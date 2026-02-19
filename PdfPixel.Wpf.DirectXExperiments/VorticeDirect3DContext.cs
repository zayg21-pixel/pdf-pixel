using SkiaSharp;
using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Direct3D9 = Vortice.Direct3D9;

namespace PdfPixel.Wpf.DirectXExperiments
{
    public class VorticeDirect3DContext : IDisposable
    {
        private IDXGIFactory4 _factory;
        private IDXGIAdapter1 _adapter;
        private ID3D12Device2 _d3d12Device;
        private ID3D11Device _d3d11Device;
        private Direct3D9.IDirect3D9Ex _d3d9;
        private Direct3D9.IDirect3DDevice9Ex _d3d9Device;
        private ID3D12CommandQueue _queue;
        private ID3D11DeviceContext _d3d11Context;
        private bool _disposed;

        private VorticeDirect3DContext() { }

        /// <summary>Creates and fully initializes a new <see cref="VorticeDirect3DContext"/>.</summary>
        public static VorticeDirect3DContext Create()
        {
            var context = new VorticeDirect3DContext();
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
            _factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            Direct3D9.D3D9.Direct3DCreate9Ex(out _d3d9);

            if (_d3d9 == null)
            {
                throw new NotSupportedException("Unable to create Direct3D 9Ex instance.");
            }

            uint d3d9AdapterCount = _d3d9.AdapterCount;
            for (uint d3d9Index = 0; d3d9Index < d3d9AdapterCount; d3d9Index++)
            {
                var d3d9Luid = _d3d9.GetAdapterLuid(d3d9Index);
                IDXGIAdapter1 candidateAdapter = null;

                for (uint dxgiIndex = 0; _factory.EnumAdapters1(dxgiIndex, out candidateAdapter).Success; dxgiIndex++)
                {
                    var adapterLuid = candidateAdapter.Description.Luid;
                    bool luidMatches = adapterLuid.LowPart == d3d9Luid.LowPart &&
                                       adapterLuid.HighPart == d3d9Luid.HighPart;

                    if (!luidMatches)
                    {
                        candidateAdapter.Dispose();
                        candidateAdapter = null;
                        continue;
                    }

                    if (!D3D12.D3D12CreateDevice(candidateAdapter, FeatureLevel.Level_11_0, out ID3D12Device2 d3d12Device).Success)
                    {
                        candidateAdapter.Dispose();
                        break;
                    }

                    var d3d11Result = D3D11.D3D11CreateDevice(
                        candidateAdapter,
                        DriverType.Unknown,
                        DeviceCreationFlags.BgraSupport,
                        new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                        out var d3d11Device,
                        out _,
                        out var d3d11Context);

                    if (!d3d11Result.Success)
                    {
                        d3d12Device.Dispose();
                        candidateAdapter.Dispose();
                        break;
                    }

                    var presentParams = new Direct3D9.PresentParameters
                    {
                        Windowed = true,
                        SwapEffect = Direct3D9.SwapEffect.Discard,
                        DeviceWindowHandle = IntPtr.Zero,
                        PresentationInterval = Direct3D9.PresentInterval.Default,
                        BackBufferFormat = Direct3D9.Format.Unknown,
                        BackBufferWidth = 1,
                        BackBufferHeight = 1
                    };

                    _d3d9Device = _d3d9.CreateDeviceEx(
                        d3d9Index,
                        Direct3D9.DeviceType.Hardware,
                        IntPtr.Zero,
                        Direct3D9.CreateFlags.HardwareVertexProcessing | Direct3D9.CreateFlags.Multithreaded | Direct3D9.CreateFlags.FpuPreserve,
                        presentParams);

                    _adapter = candidateAdapter;
                    _d3d12Device = d3d12Device;
                    _d3d11Device = d3d11Device;
                    _d3d11Context = d3d11Context;
                    _queue = _d3d12Device.CreateCommandQueue(new CommandQueueDescription
                    {
                        Flags = CommandQueueFlags.None,
                        Type = CommandListType.Direct
                    });
                    return;
                }
            }

            throw new NotSupportedException("No suitable adapter found that supports D3D9Ex, D3D11, and D3D12.");
        }

        public IDXGIFactory4 Factory => _factory;

        public IDXGIAdapter1 Adapter => _adapter;

        public ID3D12Device2 D3D12Device => _d3d12Device;

        public ID3D11Device D3D11Device => _d3d11Device;

        public ID3D11DeviceContext D3D11Context => _d3d11Context;

        public Direct3D9.IDirect3D9Ex D3D9 => _d3d9;

        public Direct3D9.IDirect3DDevice9Ex D3D9Device => _d3d9Device;

        public ID3D12CommandQueue Queue => _queue;

        public GRD3DBackendContext CreateBackendContext()
        {
            return new GRD3DBackendContext
            {
                Adapter = _adapter.NativePointer,
                Device = _d3d12Device.NativePointer,
                Queue = Queue.NativePointer
            };
        }

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
}
