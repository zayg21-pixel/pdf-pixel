using System.Diagnostics;

namespace PdfPixel.Gold.Utilities;

/// <summary>
/// Reads the process's memory and keeps the peak it reaches, sampled from a background thread.
/// </summary>
internal static class MemoryUtilities
{
    // How often the background sampler reads the process's private memory while a page renders. A
    // page that allocates and releases hundreds of megabytes is over in milliseconds, so the peak is
    // missed entirely at a coarser interval.
    private const int MemorySampleIntervalMilliseconds = 5;

    // Highest private and managed memory the process has reached since the readings were last reset,
    // written by the sampler thread and read by the page that is rendering.
    private static long _peakPrivateBytes;

    private static long _peakManagedBytes;

    /// <summary>
    /// Highest private memory reached since the last <see cref="ResetPeaks"/>.
    /// </summary>
    public static long PeakPrivateBytes => Interlocked.Read(ref _peakPrivateBytes);

    /// <summary>
    /// Highest managed heap size reached since the last <see cref="ResetPeaks"/>.
    /// </summary>
    public static long PeakManagedBytes => Interlocked.Read(ref _peakManagedBytes);

    /// <summary>
    /// Starts the background thread that raises the peaks for as long as the process runs.
    /// </summary>
    public static void StartPeakSampler()
    {
        // Peak memory is only visible while the page is still rendering, so it is watched from a
        // second thread rather than read once the render has already handed everything back.
        Thread memorySampler = new(SamplePeakMemory)
        {
            IsBackground = true,
        };

        memorySampler.Start();
    }

    /// <summary>
    /// Restarts both peaks from the given readings.
    /// </summary>
    public static void ResetPeaks(long privateBytes, long managedBytes)
    {
        Interlocked.Exchange(ref _peakPrivateBytes, privateBytes);
        Interlocked.Exchange(ref _peakManagedBytes, managedBytes);
    }

    // Private bytes is what Task Manager shows and what the process is charged against the commit
    // limit; it covers the managed heap and every native allocation Skia and the decoders make.
    public static long GetPrivateMemoryBytes()
    {
        using Process process = Process.GetCurrentProcess();

        return process.PrivateMemorySize64;
    }

    /// <summary>
    /// Raises the peak readings to what the process holds for as long as the run lasts, so that a
    /// page which allocates and releases within its own render is still charged for what it took
    /// while it held it.
    /// </summary>
    private static void SamplePeakMemory()
    {
        using Process process = Process.GetCurrentProcess();

        while (true)
        {
            process.Refresh();

            long privateBytes = process.PrivateMemorySize64;

            if (privateBytes > Interlocked.Read(ref _peakPrivateBytes))
            {
                Interlocked.Exchange(ref _peakPrivateBytes, privateBytes);
            }

            long managedBytes = GC.GetTotalMemory(forceFullCollection: false);

            if (managedBytes > Interlocked.Read(ref _peakManagedBytes))
            {
                Interlocked.Exchange(ref _peakManagedBytes, managedBytes);
            }

            Thread.Sleep(MemorySampleIntervalMilliseconds);
        }
    }
}
