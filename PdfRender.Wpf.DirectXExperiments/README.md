# PdfRender.Wpf.DirectXExperiments

This is an experimental WPF project that demonstrates DirectX-accelerated PDF rendering using D3DImage.

## Overview

This project explores using DirectX 11 with WPF's D3DImage to display GPU-rendered PDF content. The goal is to achieve better performance by leveraging DirectX interop with WPF's composition engine, avoiding CPU-side pixel copying to WritableBitmap.

## Architecture

The experiment consists of three main components:

### 1. VorticeDirect3DContext
A Direct3D 12 context wrapper (originally from PdfRender.Console.Demo) that creates and manages DirectX 12 resources for SkiaSharp GPU backend. This demonstrates the D3D12 approach used in the console demo.

### 2. DirectXImageControl
A custom WPF Image control that:
- Creates a Direct3D 11 device and context
- Creates a shared texture render target that can be displayed via D3DImage
- Uses D3DImage to display the DirectX-rendered content without CPU-side pixel copying
- Provides a `RenderPdf` method that accepts a drawing action

**Current Implementation:**
The control renders PDF content using SkiaSharp's CPU rasterizer, then uploads the result to a D3D11 texture using a staging buffer. While this still involves a memory copy, it demonstrates the D3DImage interop pattern. Future work could integrate SkiaSharp's D3D11 backend for true GPU rendering.

### 3. MainWindow
A simple WPF application that:
- Loads PDF files
- Navigates between pages
- Renders pages using the DirectXImageControl

## Key Technologies

- **Vortice.Direct3D11**: Modern .NET wrapper for DirectX 11
- **SkiaSharp**: Cross-platform 2D graphics library
- **D3DImage**: WPF's interop class for displaying DirectX content
- **PdfReader**: The core PDF rendering library

## How It Works

1. The DirectXImageControl creates a Direct3D 11 device and shared texture
2. When RenderPdf is called, PDF content is drawn to a SkiaSharp surface (CPU-based currently)
3. The pixel data is uploaded to a D3D11 staging texture
4. The staging texture is copied to the shared render target
5. The shared texture is exposed to WPF via D3DImage
6. WPF displays the texture without additional CPU-side copying

## Benefits

- **DirectX Integration**: Uses native DirectX for texture management
- **D3DImage Interop**: Avoids WritableBitmap overhead for WPF display
- **Hardware Acceleration**: WPF composition is hardware-accelerated
- **Experimental Platform**: Foundation for exploring GPU-accelerated PDF rendering

## Limitations

- D3DImage requires Direct3D 9Ex or Direct3D 11 (not D3D12)
- Current implementation still uses CPU rendering (SkiaSharp software rasterizer)
- Memory copy from CPU to GPU texture is still required
- D3DImage has specific threading requirements (must update on UI thread)
- Only supports .NET 8.0+, .NET 9.0+, and .NET 10.0+ (Vortice packages don't support .NET Framework 4.8)

## Next Steps

To further optimize and explore GPU rendering:
- Integrate SkiaSharp's Direct3D 11 backend for GPU rasterization
- Implement proper resource pooling to avoid texture recreation
- Add asynchronous rendering support
- Profile performance vs WritableBitmap approach
- Test with various PDF documents and page sizes
- Implement proper error handling and fallbacks
- Explore Direct2D/Direct3D 11 interop for native PDF rendering

## Building

The project targets:
- .NET 8.0-windows
- .NET 9.0-windows  
- .NET 10.0-windows

Use Visual Studio 2022 or later to build.

## Running

1. Open the solution in Visual Studio
2. Set PdfRender.Wpf.DirectXExperiments as the startup project
3. Run the application
4. Click "Open PDF" to load a PDF file
5. Navigate pages and observe DirectX-based rendering via D3DImage
