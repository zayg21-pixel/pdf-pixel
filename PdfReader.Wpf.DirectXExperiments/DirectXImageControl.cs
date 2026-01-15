using SkiaSharp;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;

namespace PdfReader.Wpf.DirectXExperiments
{
    /// <summary>
    /// WPF control that uses D3DImage to display DirectX-rendered content.
    /// Uses Direct3D 12 GPU rendering with SkiaSharp, shared with D3D11 via shared handle, then D3D9Ex for D3DImage interop.
    /// </summary>
    public class DirectXImageControl : Image
    {
        private D3DImage _d3dImage;
        private VorticeDirect3DContext _d3dContext;
        private GRContext _grContext;
        private SharedDirectXResources _sharedResources;
        private int _currentWidth;
        private int _currentHeight;

        public DirectXImageControl()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            InitializeDirectX();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CleanupDirectX();
        }

        private void InitializeDirectX()
        {
            if (_d3dContext != null)
            {
                return;
            }

            _d3dContext = new VorticeDirect3DContext();
            
            var backendContext = _d3dContext.CreateBackendContext();
            _grContext = GRContext.CreateDirect3D(backendContext);

            _sharedResources = new SharedDirectXResources(_d3dContext);

            _d3dImage = new D3DImage();
            Source = _d3dImage;
        }

        private void UpdateRenderTarget(int width, int height)
        {
            if (width == _currentWidth && height == _currentHeight && _sharedResources.SkiaSurface != null)
            {
                return;
            }

            _currentWidth = width;
            _currentHeight = height;

            _sharedResources.CreateSharedResources(width, height, _grContext);

            UpdateD3DImageBackBuffer();
        }

        private void UpdateD3DImageBackBuffer()
        {
            if (_sharedResources.D3D9Surface == null || _d3dImage == null)
            {
                return;
            }

            _d3dImage.Lock();
            try
            {
                _d3dImage.SetBackBuffer(D3DResourceType.IDirect3DSurface9, _sharedResources.D3D9Surface.NativePointer);
            }
            finally
            {
                _d3dImage.Unlock();
            }
        }

        public void RenderPdf(Action<SKCanvas> drawAction)
        {
            if (drawAction == null || _grContext == null)
            {
                return;
            }

            int width = (int)Math.Max(1, ActualWidth);
            int height = (int)Math.Max(1, ActualHeight);

            UpdateRenderTarget(width, height);

            var canvas = _sharedResources.SkiaSurface.Canvas;
            canvas.Clear(SKColors.White);
            drawAction(canvas);
            canvas.Flush();

            _grContext.Flush();

            _d3dImage.Lock();
            try
            {
                _d3dImage.AddDirtyRect(new Int32Rect(0, 0, width, height));
            }
            finally
            {
                _d3dImage.Unlock();
            }
        }

        private void CleanupDirectX()
        {
            _sharedResources?.Dispose();
            _sharedResources = null;

            _grContext?.Dispose();
            _grContext = null;

            _d3dContext?.Dispose();
            _d3dContext = null;
        }
    }
}
