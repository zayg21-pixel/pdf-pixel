using PdfPixel.PdfPanel.Wpf.D3D.Interop;
using SkiaSharp;
using System;
using static PdfPixel.PdfPanel.Wpf.D3D.DirectXInterop;

namespace PdfPixel.PdfPanel.Wpf.D3D;

/// <summary>
/// Stateless factory for creating the shared DirectX resource chain (D3D12 → D3D11 → D3D9) and
/// the SkiaSharp <see cref="SKSurface"/> that draws into it.
/// Callers own and dispose each returned object independently, which allows the D3DImage back buffer
/// to be updated atomically with new content before old resources are released.
/// </summary>
internal class SharedDirectXResources : IDisposable
{
    private readonly Direct3DContext _context;
    private bool _disposed;

    internal SharedDirectXResources(Direct3DContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    /// <summary>
    /// Creates a new <see cref="D3D9Texture"/> for the given dimensions.
    /// The returned instance is owned by the caller and must be disposed when no longer needed.
    /// </summary>
    internal D3D9Texture CreateD3D9Texture(int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentException("Width and height must be positive.");
        }

        var textureDesc = new D3D11Texture2DDesc
        {
            Width = (uint)width,
            Height = (uint)height,
            MipLevels = 1,
            ArraySize = 1,
            Format = DxgiFormatB8G8R8A8UNorm,
            SampleDesc = new DxgiSampleDesc { Count = 1, Quality = 0 },
            Usage = D3D11UsageDefault,
            BindFlags = D3D11BindRenderTarget | D3D11BindShaderResource,
            CpuAccessFlags = 0,
            MiscFlags = D3D11ResourceMiscShared
        };

        ThrowIfFailed(D3D11DeviceCreateTexture2D(_context.D3D11DevicePointer, ref textureDesc, out var d3d11TexturePtr));
        var d3d11Texture = new DirectXHandle(d3d11TexturePtr);

        ThrowIfFailed(ComQueryInterface(d3d11TexturePtr, ref IidDxgiResource, out var dxgiResourcePtr));
        try
        {
            ThrowIfFailed(DxgiResourceGetSharedHandle(dxgiResourcePtr, out var sharedHandle));

            ThrowIfFailed(D3D12DeviceOpenSharedHandle(_context.D3D12DevicePointer, sharedHandle, out var d3d12ResourcePtr));
            var d3d12Resource = new DirectXHandle(d3d12ResourcePtr);

            ThrowIfFailed(D3D9DeviceCreateRenderTarget(
                _context.D3D9DevicePointer,
                (uint)width,
                (uint)height,
                D3D9FormatA8R8G8B8,
                D3D9MultisampleNone,
                0,
                0,
                out var d3d9SurfacePtr,
                ref sharedHandle));
            var d3d9Surface = new DirectXHandle(d3d9SurfacePtr);

            return new D3D9Texture(d3d12Resource, d3d11Texture, d3d9Surface);
        }
        finally
        {
            ComRelease(dxgiResourcePtr);
        }
    }

    /// <summary>
    /// Creates an <see cref="SKSurface"/> backed by the D3D12 resource inside <paramref name="texture"/>.
    /// The returned surface is owned by the caller and must be disposed when no longer needed.
    /// </summary>
    internal SKSurface CreateSurface(D3D9Texture texture, int width, int height, GRContext grContext)
    {
        if (texture == null)
        {
            throw new ArgumentNullException(nameof(texture));
        }

        if (grContext == null)
        {
            throw new ArgumentNullException(nameof(grContext));
        }

        var renderTargetInfo = new GRD3DTextureResourceInfo
        {
            Resource = texture.D3D12ResourcePointer,
            ResourceState = D3D12ResourceStateRenderTarget,
            Format = DxgiFormatB8G8R8A8UNorm,
            SampleCount = 1,
            LevelCount = 1,
            SampleQualityPattern = 0,
            Protected = false
        };

        using var backendRenderTarget = new GRBackendRenderTarget(width, height, renderTargetInfo);

        return SKSurface.Create(
            grContext,
            backendRenderTarget,
            GRSurfaceOrigin.TopLeft,
            SKColorType.Bgra8888);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
    }
}
