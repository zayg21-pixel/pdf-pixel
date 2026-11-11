using System.Diagnostics;
using System.Globalization;
using System.Runtime;
using System.Text;

namespace PdfPixel.Diagnostics;

/// <summary>
/// Records the memory of this process while a workload runs: the private memory and the managed heap, garbage
/// included, at a fixed interval, and what is alive after every full collection, the only collection that looks at
/// every generation and the large object heap.
/// </summary>
public sealed class MemorySampler
{
    private const int SampleMilliseconds = 50;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    private const int LargeObjectHeapGeneration = 3;

    private readonly List<MemorySample> _samples = [];
    private readonly List<FullCollection> _fullCollections = [];
    private readonly CancellationTokenSource _cancellation = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Task _samplingTask;
    private long _lastBlockingIndex;
    private long _lastBackgroundIndex;

    private MemorySampler()
    {
        _lastBlockingIndex = GC.GetGCMemoryInfo(GCKind.FullBlocking).Index;
        _lastBackgroundIndex = GC.GetGCMemoryInfo(GCKind.Background).Index;
        _samplingTask = Task.Run(Sample);
    }

    /// <summary>
    /// Starts recording.
    /// </summary>
    public static MemorySampler Start() => new();

    /// <summary>
    /// Stops recording, collects the whole heap compacted, records what is alive after it as what the run retained,
    /// prints the summary and writes the records to <paramref name="outputDirectory"/>.
    /// </summary>
    public void Finish(string outputDirectory)
    {
        _cancellation.Cancel();
        _samplingTask.Wait();

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, blocking: true, compacting: true);
        FullCollection retained = CreateFullCollection(GC.GetGCMemoryInfo(GCKind.FullBlocking), "retained");

        string samplesPath = Path.Combine(outputDirectory, "memory-samples.csv");
        string collectionsPath = Path.Combine(outputDirectory, "memory-full-collections.csv");
        WriteSamples(samplesPath);
        WriteFullCollections(collectionsPath, retained);
        PrintSummary(retained, samplesPath, collectionsPath);
    }

    private void Sample()
    {
        using Process process = Process.GetCurrentProcess();

        while (!_cancellation.IsCancellationRequested)
        {
            process.Refresh();
            _samples.Add(new MemorySample(_stopwatch.Elapsed.TotalMilliseconds, process.PrivateMemorySize64, GC.GetTotalMemory(forceFullCollection: false)));

            _lastBlockingIndex = RecordFullCollection(GCKind.FullBlocking, _lastBlockingIndex, "blocking");
            _lastBackgroundIndex = RecordFullCollection(GCKind.Background, _lastBackgroundIndex, "background");

            _cancellation.Token.WaitHandle.WaitOne(SampleMilliseconds);
        }
    }

    // A full collection that ran between two samples is still the last one of its kind the runtime reports, so it is
    // recorded when its index differs from the last one recorded; a second one of the same kind within one interval
    // is missed. Returns the index of the last collection of the kind.
    private long RecordFullCollection(GCKind kind, long lastIndex, string kindName)
    {
        GCMemoryInfo info = GC.GetGCMemoryInfo(kind);

        if (info.Index != lastIndex)
        {
            _fullCollections.Add(CreateFullCollection(info, kindName));
        }

        return info.Index;
    }

    private FullCollection CreateFullCollection(in GCMemoryInfo info, string kindName)
    {
        GCGenerationInfo largeObjectHeap = info.GenerationInfo[LargeObjectHeapGeneration];

        return new FullCollection(
            _stopwatch.Elapsed.TotalMilliseconds,
            info.Index,
            kindName,
            info.HeapSizeBytes - info.FragmentedBytes,
            info.FragmentedBytes,
            largeObjectHeap.SizeAfterBytes - largeObjectHeap.FragmentationAfterBytes,
            largeObjectHeap.FragmentationAfterBytes);
    }

    private void WriteSamples(string path)
    {
        StringBuilder builder = new();
        builder.AppendLine("milliseconds,private_mb,managed_with_garbage_mb");

        foreach (MemorySample sample in _samples)
        {
            builder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"{sample.Milliseconds:F0},{ToMegabytes(sample.PrivateBytes):F1},{ToMegabytes(sample.ManagedBytes):F1}"));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void WriteFullCollections(string path, FullCollection retained)
    {
        StringBuilder builder = new();
        builder.AppendLine("milliseconds,index,kind,live_mb,fragmentation_mb,large_object_heap_live_mb,large_object_heap_fragmentation_mb");

        foreach (FullCollection collection in _fullCollections.Append(retained))
        {
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{collection.Milliseconds:F0},{collection.Index},{collection.Kind},{ToMegabytes(collection.LiveBytes):F1},{ToMegabytes(collection.FragmentedBytes):F1},{ToMegabytes(collection.LargeObjectHeapLiveBytes):F1},{ToMegabytes(collection.LargeObjectHeapFragmentedBytes):F1}"));
        }

        File.WriteAllText(path, builder.ToString());
    }

    private void PrintSummary(FullCollection retained, string samplesPath, string collectionsPath)
    {
        Console.WriteLine($"Private memory peaked at {ToMegabytes(_samples.Max(static sample => sample.PrivateBytes)):F1} MB, the managed heap with its garbage at {ToMegabytes(_samples.Max(static sample => sample.ManagedBytes)):F1} MB, sampled every {SampleMilliseconds} ms");

        if (_fullCollections.Count > 0)
        {
            Console.WriteLine($"Alive after the {_fullCollections.Count} full collection(s): at most {ToMegabytes(_fullCollections.Max(static collection => collection.LiveBytes)):F1} MB, of it {ToMegabytes(_fullCollections.Max(static collection => collection.LargeObjectHeapLiveBytes)):F1} MB on the large object heap");
        }

        Console.WriteLine($"Retained at the end: {ToMegabytes(retained.LiveBytes):F1} MB alive, of it {ToMegabytes(retained.LargeObjectHeapLiveBytes):F1} MB on the large object heap");
        Console.WriteLine($"Records written to {samplesPath} and {collectionsPath}");
    }

    private static double ToMegabytes(long bytes) => bytes / BytesPerMegabyte;

    private readonly record struct MemorySample(double Milliseconds, long PrivateBytes, long ManagedBytes);

    private readonly record struct FullCollection(
        double Milliseconds,
        long Index,
        string Kind,
        long LiveBytes,
        long FragmentedBytes,
        long LargeObjectHeapLiveBytes,
        long LargeObjectHeapFragmentedBytes);
}
