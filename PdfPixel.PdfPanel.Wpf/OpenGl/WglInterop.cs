using System;
using System.Runtime.InteropServices;

namespace PdfPixel.PdfPanel.Wpf.OpenGl;

/// <summary>
/// P/Invoke declarations for creating an offscreen WGL (Windows OpenGL) context.
/// </summary>
internal static class WglInterop
{
    private const string Gdi32 = "gdi32.dll";
    private const string OpenGl32 = "opengl32.dll";
    private const string User32 = "user32.dll";
    private const string Kernel32 = "kernel32.dll";

    // Pixel format descriptor flags
    internal const uint PfdDrawToWindow = 0x00000004;
    internal const uint PfdSupportOpengl = 0x00000020;
    internal const uint PfdDoubleBuffer = 0x00000001;
    internal const byte PfdTypeRgba = 0;
    internal const byte PfdMainPlane = 0;

    // Window styles
    internal const uint WsPopup = 0x80000000;

    [StructLayout(LayoutKind.Sequential)]
    internal struct PixelFormatDescriptor
    {
        public ushort Size;
        public ushort Version;
        public uint Flags;
        public byte PixelType;
        public byte ColorBits;
        public byte RedBits;
        public byte RedShift;
        public byte GreenBits;
        public byte GreenShift;
        public byte BlueBits;
        public byte BlueShift;
        public byte AlphaBits;
        public byte AlphaShift;
        public byte AccumBits;
        public byte AccumRedBits;
        public byte AccumGreenBits;
        public byte AccumBlueBits;
        public byte AccumAlphaBits;
        public byte DepthBits;
        public byte StencilBits;
        public byte AuxBuffers;
        public byte LayerType;
        public byte Reserved;
        public uint LayerMask;
        public uint VisibleMask;
        public uint DamageMask;
    }

    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr CreateWindowExW(
        uint exStyle,
        [MarshalAs(UnmanagedType.LPWStr)] string className,
        [MarshalAs(UnmanagedType.LPWStr)] string windowName,
        uint style,
        int x, int y, int width, int height,
        IntPtr parent, IntPtr menu, IntPtr instance, IntPtr param);

    [DllImport(User32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport(User32, SetLastError = true)]
    internal static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    [DllImport(Gdi32, SetLastError = true)]
    internal static extern int ChoosePixelFormat(IntPtr hDC, ref PixelFormatDescriptor ppfd);

    [DllImport(Gdi32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetPixelFormat(IntPtr hDC, int format, ref PixelFormatDescriptor ppfd);

    [DllImport(OpenGl32, SetLastError = true)]
    internal static extern IntPtr wglCreateContext(IntPtr hDC);

    [DllImport(OpenGl32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool wglMakeCurrent(IntPtr hDC, IntPtr hGLRC);

    [DllImport(OpenGl32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool glEnable(uint cap);

    [DllImport(OpenGl32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool glHint(uint target, uint mode);

    [DllImport(OpenGl32, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool wglDeleteContext(IntPtr hGLRC);

    [DllImport(OpenGl32)]
    internal static extern IntPtr wglGetCurrentContext();

    [DllImport(Kernel32)]
    internal static extern IntPtr GetModuleHandleW([MarshalAs(UnmanagedType.LPWStr)] string moduleName);
}
