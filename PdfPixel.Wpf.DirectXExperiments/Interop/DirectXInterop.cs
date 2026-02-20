using System;
using System.Runtime.InteropServices;

namespace PdfPixel.Wpf.DirectXExperiments.Interop
{
    /// <summary>
    /// Minimal P/Invoke and COM vtable interop for Direct3D 9Ex, 11, 12, and DXGI.
    /// All COM objects are represented as raw <see cref="IntPtr"/> handles wrapped in
    /// <see cref="DirectXHandle"/> for deterministic release.
    /// </summary>
    internal static unsafe class DirectXInterop
    {
        #region Constants

        /// <summary>D3D_SDK_VERSION for Direct3D 9.</summary>
        internal const uint D3D9SdkVersion = 32;

        /// <summary>D3D11_SDK_VERSION.</summary>
        internal const uint D3D11SdkVersion = 7;

        // D3D_FEATURE_LEVEL
        internal const int FeatureLevel110 = 0xb000;
        internal const int FeatureLevel111 = 0xb100;

        // DXGI_FORMAT
        internal const uint DxgiFormatUnknown = 0;
        internal const uint DxgiFormatB8G8R8A8UNorm = 87;

        // D3D11_USAGE
        internal const uint D3D11UsageDefault = 0;

        // D3D11 bind flags
        internal const uint D3D11BindShaderResource = 0x8;
        internal const uint D3D11BindRenderTarget = 0x20;

        // D3D11 resource misc flags
        internal const uint D3D11ResourceMiscShared = 0x2;

        // D3D11_CREATE_DEVICE_FLAG
        internal const uint D3D11CreateDeviceBgraSupport = 0x20;

        // D3D_DRIVER_TYPE
        internal const uint D3D11DriverTypeUnknown = 0;

        // D3D12_COMMAND_LIST_TYPE
        internal const int D3D12CommandListTypeDirect = 0;

        // D3D12_COMMAND_QUEUE_FLAGS
        internal const uint D3D12CommandQueueFlagNone = 0;

        // D3D12_RESOURCE_STATES
        internal const uint D3D12ResourceStateRenderTarget = 0x4;

        // D3DFORMAT
        internal const uint D3D9FormatUnknown = 0;
        internal const uint D3D9FormatA8R8G8B8 = 21;

        // D3DMULTISAMPLE_TYPE
        internal const uint D3D9MultisampleNone = 0;

        // D3DSWAPEFFECT
        internal const uint D3D9SwapEffectDiscard = 1;

        // D3DPRESENT_INTERVAL
        internal const uint D3D9PresentIntervalDefault = 0;

        // D3DDEVTYPE
        internal const uint D3D9DeviceTypeHal = 1;

        // D3DCREATE flags
        internal const uint D3D9CreateHardwareVertexProcessing = 0x40;
        internal const uint D3D9CreateMultithreaded = 0x4;
        internal const uint D3D9CreateFpuPreserve = 0x2;

        #endregion

        #region GUIDs

        internal static Guid IidDxgiFactory1 = new("770aae78-f26f-4dba-a829-253c83d1b387");
        internal static Guid IidDxgiResource = new("035f3ab4-482e-4e50-b41f-8a7f8bd8960b");
        internal static Guid IidD3D12Device = new("189819f1-1db6-4b57-be54-1821339b85f7");
        internal static Guid IidD3D12Resource = new("696442be-a72e-4059-bc79-5b5c98040fad");
        internal static Guid IidD3D12CommandQueue = new("0ec870a6-5d7e-4c22-8cfc-5baae07616ed");

        #endregion

        #region Structs

        [StructLayout(LayoutKind.Sequential)]
        internal struct Luid
        {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DxgiAdapterDesc1
        {
            public fixed char Description[128];
            public uint VendorId;
            public uint DeviceId;
            public uint SubSysId;
            public uint Revision;
            public nuint DedicatedVideoMemory;
            public nuint DedicatedSystemMemory;
            public nuint SharedSystemMemory;
            public Luid AdapterLuid;
            public uint Flags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct DxgiSampleDesc
        {
            public uint Count;
            public uint Quality;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3D11Texture2DDesc
        {
            public uint Width;
            public uint Height;
            public uint MipLevels;
            public uint ArraySize;
            public uint Format;
            public DxgiSampleDesc SampleDesc;
            public uint Usage;
            public uint BindFlags;
            public uint CpuAccessFlags;
            public uint MiscFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3D12CommandQueueDesc
        {
            public int Type;
            public int Priority;
            public uint Flags;
            public uint NodeMask;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct D3DPresentParameters
        {
            public uint BackBufferWidth;
            public uint BackBufferHeight;
            public uint BackBufferFormat;
            public uint BackBufferCount;
            public uint MultiSampleType;
            public uint MultiSampleQuality;
            public uint SwapEffect;
            public IntPtr DeviceWindow;
            public int Windowed;
            public int EnableAutoDepthStencil;
            public uint AutoDepthStencilFormat;
            public uint Flags;
            public uint FullScreenRefreshRateInHz;
            public uint PresentationInterval;
        }

        #endregion

        #region P/Invoke

        [DllImport("dxgi.dll", ExactSpelling = true)]
        private static extern int CreateDXGIFactory1(ref Guid riid, out IntPtr factory);

        [DllImport("d3d12.dll", ExactSpelling = true)]
        private static extern int D3D12CreateDevice(
            IntPtr adapter,
            int minimumFeatureLevel,
            ref Guid riid,
            out IntPtr device);

        [DllImport("d3d11.dll", ExactSpelling = true)]
        private static extern int D3D11CreateDevice(
            IntPtr adapter,
            uint driverType,
            IntPtr software,
            uint flags,
            [MarshalAs(UnmanagedType.LPArray)] int[] featureLevels,
            uint featureLevelCount,
            uint sdkVersion,
            out IntPtr device,
            out int featureLevel,
            out IntPtr immediateContext);

        [DllImport("d3d9.dll", ExactSpelling = true)]
        private static extern int Direct3DCreate9Ex(uint sdkVersion, out IntPtr d3d9);

        #endregion

        #region IUnknown

        /// <summary>Calls <c>IUnknown::QueryInterface</c> (vtable slot 0).</summary>
        internal static int ComQueryInterface(IntPtr comObject, ref Guid riid, out IntPtr result)
        {
            IntPtr* vtable = *(IntPtr**)comObject;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, Guid*, IntPtr*, int>)vtable[0];
            IntPtr output;
            fixed (Guid* iid = &riid)
            {
                int hr = fn(comObject, iid, &output);
                result = hr >= 0 ? output : IntPtr.Zero;
                return hr;
            }
        }

        /// <summary>Calls <c>IUnknown::Release</c> (vtable slot 2).</summary>
        internal static uint ComRelease(IntPtr comObject)
        {
            IntPtr* vtable = *(IntPtr**)comObject;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint>)vtable[2];
            return fn(comObject);
        }

        #endregion

        #region DXGI

        /// <summary>Creates a DXGI factory (IDXGIFactory1).</summary>
        internal static int CreateFactory(out IntPtr factory)
        {
            return CreateDXGIFactory1(ref IidDxgiFactory1, out factory);
        }

        /// <summary><c>IDXGIFactory1::EnumAdapters1</c> (vtable slot 12).</summary>
        internal static int DxgiFactoryEnumAdapters1(IntPtr factory, uint index, out IntPtr adapter)
        {
            IntPtr* vtable = *(IntPtr**)factory;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, IntPtr*, int>)vtable[12];
            IntPtr output;
            int hr = fn(factory, index, &output);
            adapter = hr >= 0 ? output : IntPtr.Zero;
            return hr;
        }

        /// <summary>
        /// Gets the adapter LUID from <c>IDXGIAdapter1::GetDesc1</c> (vtable slot 10).
        /// Only the <see cref="Luid"/> is extracted; remaining descriptor fields are discarded.
        /// </summary>
        internal static int DxgiAdapterGetLuid(IntPtr adapter, out Luid luid)
        {
            IntPtr* vtable = *(IntPtr**)adapter;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, DxgiAdapterDesc1*, int>)vtable[10];
            DxgiAdapterDesc1 desc;
            int hr = fn(adapter, &desc);
            luid = hr >= 0 ? desc.AdapterLuid : default;
            return hr;
        }

        /// <summary><c>IDXGIResource::GetSharedHandle</c> (vtable slot 8).</summary>
        internal static int DxgiResourceGetSharedHandle(IntPtr resource, out IntPtr sharedHandle)
        {
            IntPtr* vtable = *(IntPtr**)resource;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr*, int>)vtable[8];
            IntPtr output;
            int hr = fn(resource, &output);
            sharedHandle = hr >= 0 ? output : IntPtr.Zero;
            return hr;
        }

        #endregion

        #region D3D9

        /// <summary>Creates a Direct3D 9Ex instance.</summary>
        internal static int CreateD3D9Ex(out IntPtr d3d9)
        {
            return Direct3DCreate9Ex(D3D9SdkVersion, out d3d9);
        }

        /// <summary><c>IDirect3D9::GetAdapterCount</c> (vtable slot 4).</summary>
        internal static uint D3D9GetAdapterCount(IntPtr d3d9)
        {
            IntPtr* vtable = *(IntPtr**)d3d9;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint>)vtable[4];
            return fn(d3d9);
        }

        /// <summary><c>IDirect3D9Ex::GetAdapterLUID</c> (vtable slot 21).</summary>
        internal static int D3D9GetAdapterLuid(IntPtr d3d9, uint adapter, out Luid luid)
        {
            IntPtr* vtable = *(IntPtr**)d3d9;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, Luid*, int>)vtable[21];
            Luid output;
            int hr = fn(d3d9, adapter, &output);
            luid = hr >= 0 ? output : default;
            return hr;
        }

        /// <summary><c>IDirect3D9Ex::CreateDeviceEx</c> (vtable slot 20).</summary>
        internal static int D3D9CreateDeviceEx(
            IntPtr d3d9,
            uint adapter,
            uint deviceType,
            IntPtr focusWindow,
            uint behaviorFlags,
            ref D3DPresentParameters presentParams,
            out IntPtr device)
        {
            IntPtr* vtable = *(IntPtr**)d3d9;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, IntPtr, uint, D3DPresentParameters*, IntPtr, IntPtr*, int>)vtable[20];
            IntPtr output;
            fixed (D3DPresentParameters* pp = &presentParams)
            {
                int hr = fn(d3d9, adapter, deviceType, focusWindow, behaviorFlags, pp, IntPtr.Zero, &output);
                device = hr >= 0 ? output : IntPtr.Zero;
                return hr;
            }
        }

        /// <summary><c>IDirect3DDevice9::CreateRenderTarget</c> (vtable slot 28).</summary>
        internal static int D3D9DeviceCreateRenderTarget(
            IntPtr device,
            uint width,
            uint height,
            uint format,
            uint multiSample,
            uint multiSampleQuality,
            int lockable,
            out IntPtr surface,
            ref IntPtr sharedHandle)
        {
            IntPtr* vtable = *(IntPtr**)device;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, uint, uint, uint, uint, uint, int, IntPtr*, IntPtr*, int>)vtable[28];
            IntPtr surfaceOutput;
            fixed (IntPtr* sh = &sharedHandle)
            {
                int hr = fn(device, width, height, format, multiSample, multiSampleQuality, lockable, &surfaceOutput, sh);
                surface = hr >= 0 ? surfaceOutput : IntPtr.Zero;
                return hr;
            }
        }

        #endregion

        #region D3D11

        /// <summary>Creates a D3D11 device and immediate context.</summary>
        internal static int CreateD3D11Device(
            IntPtr adapter,
            uint flags,
            int[] featureLevels,
            out IntPtr device,
            out IntPtr immediateContext)
        {
            return D3D11CreateDevice(
                adapter,
                D3D11DriverTypeUnknown,
                IntPtr.Zero,
                flags,
                featureLevels,
                (uint)featureLevels.Length,
                D3D11SdkVersion,
                out device,
                out _,
                out immediateContext);
        }

        /// <summary><c>ID3D11Device::CreateTexture2D</c> (vtable slot 5).</summary>
        internal static int D3D11DeviceCreateTexture2D(
            IntPtr device,
            ref D3D11Texture2DDesc desc,
            out IntPtr texture)
        {
            IntPtr* vtable = *(IntPtr**)device;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, D3D11Texture2DDesc*, IntPtr, IntPtr*, int>)vtable[5];
            IntPtr output;
            fixed (D3D11Texture2DDesc* d = &desc)
            {
                int hr = fn(device, d, IntPtr.Zero, &output);
                texture = hr >= 0 ? output : IntPtr.Zero;
                return hr;
            }
        }

        #endregion

        #region D3D12

        /// <summary>Creates a D3D12 device.</summary>
        internal static int CreateD3D12Device(IntPtr adapter, int featureLevel, out IntPtr device)
        {
            return D3D12CreateDevice(adapter, featureLevel, ref IidD3D12Device, out device);
        }

        /// <summary><c>ID3D12Device::CreateCommandQueue</c> (vtable slot 8).</summary>
        internal static int D3D12DeviceCreateCommandQueue(
            IntPtr device,
            ref D3D12CommandQueueDesc desc,
            out IntPtr commandQueue)
        {
            IntPtr* vtable = *(IntPtr**)device;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, D3D12CommandQueueDesc*, Guid*, IntPtr*, int>)vtable[8];
            IntPtr output;
            Guid iid = IidD3D12CommandQueue;
            fixed (D3D12CommandQueueDesc* d = &desc)
            {
                int hr = fn(device, d, &iid, &output);
                commandQueue = hr >= 0 ? output : IntPtr.Zero;
                return hr;
            }
        }

        /// <summary><c>ID3D12Device::OpenSharedHandle</c> (vtable slot 32).</summary>
        internal static int D3D12DeviceOpenSharedHandle(
            IntPtr device,
            IntPtr sharedHandle,
            out IntPtr resource)
        {
            IntPtr* vtable = *(IntPtr**)device;
            var fn = (delegate* unmanaged[Stdcall]<IntPtr, IntPtr, Guid*, IntPtr*, int>)vtable[32];
            IntPtr output;
            Guid iid = IidD3D12Resource;
            int hr = fn(device, sharedHandle, &iid, &output);
            resource = hr >= 0 ? output : IntPtr.Zero;
            return hr;
        }

        #endregion

        #region Helpers

        /// <summary>Returns <see langword="true"/> when <paramref name="hr"/> indicates success (≥ 0).</summary>
        internal static bool Succeeded(int hr) => hr >= 0;

        /// <summary>Throws a <see cref="COMException"/> when <paramref name="hr"/> indicates failure.</summary>
        internal static void ThrowIfFailed(int hr)
        {
            if (hr < 0)
            {
                Marshal.ThrowExceptionForHR(hr);
            }
        }

        #endregion
    }
}
