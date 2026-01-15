using SkiaSharp;
using System;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Vortice.DXGI;
using Direct3D9 = Vortice.Direct3D9;

namespace PdfReader.Wpf.DirectXExperiments
{
    public class VorticeDirect3DContext : IDisposable
    {
        private readonly IDXGIFactory4 _factory;
        private readonly IDXGIAdapter1 _adapter;
        private readonly ID3D12Device2 _d3d12Device;
        private readonly ID3D11Device _d3d11Device;
        private readonly Direct3D9.IDirect3D9Ex _d3d9;
        private readonly Direct3D9.IDirect3DDevice9Ex _d3d9Device;
        private ID3D12CommandQueue _queue;
        private ID3D11DeviceContext _d3d11Context;
        private bool _disposed;

        public VorticeDirect3DContext()
        {
            var factory = DXGI.CreateDXGIFactory1<IDXGIFactory4>();

            IDXGIAdapter1 adapter = null;
            ID3D12Device2 d3d12Device = null;
            ID3D11Device d3d11Device = null;
            ID3D11DeviceContext d3d11Context = null;
            Direct3D9.IDirect3D9Ex d3d9 = null;
            Direct3D9.IDirect3DDevice9Ex d3d9Device = null;
            
            using (var factory6 = factory.QueryInterfaceOrNull<IDXGIFactory6>())
            {
                if (factory6 != null)
                {
                    for (uint i = 0; factory6.EnumAdapterByGpuPreference(i, GpuPreference.HighPerformance, out adapter).Success; i++)
                    {
                        if (D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out d3d12Device).Success)
                        {
                            var creationFlags = DeviceCreationFlags.BgraSupport;
                            var result = D3D11.D3D11CreateDevice(
                                adapter,
                                DriverType.Unknown,
                                creationFlags,
                                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                                out d3d11Device,
                                out _,
                                out d3d11Context);

                            if (result.Success)
                            {
                                break;
                            }
                        }

                        d3d12Device?.Dispose();
                        d3d11Device?.Dispose();
                        d3d11Context?.Dispose();
                        adapter?.Dispose();
                        adapter = null;
                        d3d12Device = null;
                        d3d11Device = null;
                        d3d11Context = null;
                    }
                }
                else
                {
                    for (uint i = 0; factory.EnumAdapters1(i, out adapter).Success; i++)
                    {
                        if (D3D12.D3D12CreateDevice(adapter, FeatureLevel.Level_11_0, out d3d12Device).Success)
                        {
                            var creationFlags = DeviceCreationFlags.BgraSupport;
                            var result = D3D11.D3D11CreateDevice(
                                adapter,
                                DriverType.Unknown,
                                creationFlags,
                                new[] { FeatureLevel.Level_11_1, FeatureLevel.Level_11_0 },
                                out d3d11Device,
                                out _,
                                out d3d11Context);

                            if (result.Success)
                            {
                                break;
                            }
                        }

                        d3d12Device?.Dispose();
                        d3d11Device?.Dispose();
                        d3d11Context?.Dispose();
                        adapter?.Dispose();
                        adapter = null;
                        d3d12Device = null;
                        d3d11Device = null;
                        d3d11Context = null;
                    }
                }
            }

            _factory = factory;
            _adapter = adapter ?? throw new NotSupportedException("No suitable graphics adapter found.");
            _d3d12Device = d3d12Device ?? throw new NotSupportedException("Unable to create Direct3D 12 device.");
            _d3d11Device = d3d11Device ?? throw new NotSupportedException("Unable to create Direct3D 11 device.");
            _d3d11Context = d3d11Context;

            Direct3D9.D3D9.Direct3DCreate9Ex(out d3d9);

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

            d3d9Device = d3d9.CreateDeviceEx(
                0,
                Direct3D9.DeviceType.Hardware,
                IntPtr.Zero,
                Direct3D9.CreateFlags.HardwareVertexProcessing | Direct3D9.CreateFlags.Multithreaded | Direct3D9.CreateFlags.FpuPreserve,
                presentParams);

            _d3d9 = d3d9;
            _d3d9Device = d3d9Device;
        }

        public IDXGIFactory4 Factory => _factory;

        public IDXGIAdapter1 Adapter => _adapter;

        public ID3D12Device2 D3D12Device => _d3d12Device;

        public ID3D11Device D3D11Device => _d3d11Device;

        public ID3D11DeviceContext D3D11Context => _d3d11Context;

        public Direct3D9.IDirect3D9Ex D3D9 => _d3d9;

        public Direct3D9.IDirect3DDevice9Ex D3D9Device => _d3d9Device;

        public ID3D12CommandQueue Queue =>
            _queue ??= _d3d12Device.CreateCommandQueue(new CommandQueueDescription
            {
                Flags = CommandQueueFlags.None,
                Type = CommandListType.Direct
            });

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
