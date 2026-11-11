using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Runtime.InteropServices;

namespace PdfPixel.Fonts.Substitution;

/// <summary>
/// Creates the <see cref="PdfFontSubstitutor"/> loading the fonts installed on the current operating system.
/// </summary>
public static class PdfFontSubstitutorFactory
{
    private static readonly OSPlatform Android = OSPlatform.Create("ANDROID");

    /// <summary>
    /// Creates a substitutor loading the fonts installed on the current operating system, with its default user
    /// interface family as the fallback: Segoe UI on Windows, Helvetica on macOS, Roboto on Android and DejaVu Sans on
    /// Linux.
    /// </summary>
    /// <param name="loggerFactory">Logger factory used for structured diagnostics during parsing.</param>
    /// <exception cref="PlatformNotSupportedException">The operating system has no font directories to read.</exception>
    public static PdfFontSubstitutor Create(ILoggerFactory loggerFactory)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return CreateWindows(loggerFactory);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return CreateMacOS(loggerFactory);
        }
        else if (RuntimeInformation.IsOSPlatform(Android))
        {
            return new PdfFontSubstitutor(["/system/fonts"], "Roboto", loggerFactory);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return CreateLinux(loggerFactory);
        }
        else
        {
            throw new PlatformNotSupportedException("The operating system has no font directories to read.");
        }
    }

    private static PdfFontSubstitutor CreateWindows(ILoggerFactory loggerFactory)
    {
        string[] directories =
        [
            Environment.GetFolderPath(Environment.SpecialFolder.Fonts),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Windows", "Fonts")
        ];

        return new PdfFontSubstitutor(directories, "Segoe UI", loggerFactory);
    }

    private static PdfFontSubstitutor CreateMacOS(ILoggerFactory loggerFactory)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] directories = ["/System/Library/Fonts", "/Library/Fonts", Path.Combine(userProfile, "Library", "Fonts")];

        return new PdfFontSubstitutor(directories, "Helvetica", loggerFactory);
    }

    private static PdfFontSubstitutor CreateLinux(ILoggerFactory loggerFactory)
    {
        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        string[] directories =
        [
            "/usr/share/fonts",
            "/usr/local/share/fonts",
            Path.Combine(userProfile, ".local", "share", "fonts"),
            Path.Combine(userProfile, ".fonts")
        ];

        return new PdfFontSubstitutor(directories, "DejaVu Sans", loggerFactory);
    }
}
