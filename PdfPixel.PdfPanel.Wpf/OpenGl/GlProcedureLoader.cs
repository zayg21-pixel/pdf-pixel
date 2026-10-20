using System;
using System.Runtime.InteropServices;
using static PdfPixel.PdfPanel.Wpf.OpenGl.WglInterop;

namespace PdfPixel.PdfPanel.Wpf.OpenGl;

/// <summary>
/// Resolves OpenGL procedures of the current WGL context for Skia, reporting the advanced blend equation extensions as absent.
/// </summary>
internal sealed class GlProcedureLoader
{
    private const string OpenGl32 = "opengl32.dll";
    private const string GetStringiName = "glGetStringi";
    private const uint GlExtensions = 0x1F03;

    private static readonly string[] HiddenExtensions =
    [
        "GL_KHR_blend_equation_advanced",
        "GL_KHR_blend_equation_advanced_coherent"
    ];

    private static readonly nint HiddenExtensionName = Marshal.StringToHGlobalAnsi("GL_PDFPIXEL_hidden_extension");

    private readonly nint _openGlModule;
    private readonly GlGetStringi? _getStringi;
    private readonly GlGetStringi _filteredGetStringi;
    private readonly nint _filteredGetStringiPointer;

    /// <summary>
    /// Initializes the loader for the WGL context current on the calling thread.
    /// </summary>
    public GlProcedureLoader()
    {
        _openGlModule = GetModuleHandleW(OpenGl32);

        nint getStringiPointer = GetProcedureAddress(GetStringiName);

        if (getStringiPointer != 0)
        {
            _getStringi = Marshal.GetDelegateForFunctionPointer<GlGetStringi>(getStringiPointer);
        }

        _filteredGetStringi = GetFilteredString;
        _filteredGetStringiPointer = Marshal.GetFunctionPointerForDelegate(_filteredGetStringi);
    }

    /// <summary>
    /// Returns the address of the OpenGL procedure <paramref name="name"/>, or zero when it is not available.
    /// </summary>
    public nint GetProcAddress(string name)
    {
        if (name == GetStringiName)
        {
            return (_getStringi == null) ? 0 : _filteredGetStringiPointer;
        }

        return GetProcedureAddress(name);
    }

    /// <summary>
    /// wglGetProcAddress resolves extension and post-1.1 procedures only and may return 1, 2, 3 or -1 on failure;
    /// OpenGL 1.1 procedures are exported by opengl32.dll itself.
    /// </summary>
    private nint GetProcedureAddress(string name)
    {
        nint address = wglGetProcAddress(name);

        if (address >= -1 && address <= 3)
        {
            return WglInterop.GetProcAddress(_openGlModule, name);
        }

        return address;
    }

    private nint GetFilteredString(uint name, uint index)
    {
        if (_getStringi == null)
        {
            return 0;
        }

        nint value = _getStringi(name, index);

        if (name != GlExtensions || value == 0)
        {
            return value;
        }

        string? extension = Marshal.PtrToStringAnsi(value);

        if (Array.IndexOf(HiddenExtensions, extension) >= 0)
        {
            return HiddenExtensionName;
        }

        return value;
    }

    [UnmanagedFunctionPointer(CallingConvention.Winapi)]
    private delegate nint GlGetStringi(uint name, uint index);
}
