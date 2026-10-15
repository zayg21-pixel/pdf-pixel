using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Text.RegularExpressions;

namespace PdfPixel.Diagnostics;

/// <summary>
/// Runs a workload under a runtime event session and reports where it spent its time or what it
/// allocated. The trace is collected from this process itself, so no external profiler is needed.
/// </summary>
internal static class Profiler
{
    private const int TopCount = 25;

    private const int CircularBufferMegabytes = 256;

    private const double BytesPerMegabyte = 1024.0 * 1024.0;

    // A heap report row: bytes per instance, how many are alive, then the type and the module it is in.
    private static readonly Regex HeapReportRow = new(@"^\s*([\d,]+)\s+([\d,]+)\s+(.+?)\s*(\[[^\]]+\])?\s*$", RegexOptions.Compiled);

    // The report splits one array type across rows by instance size; the suffix saying which is dropped.
    private static readonly Regex SizeBucketSuffix = new(@"\s*\(Bytes > \S+\)\s*$", RegexOptions.Compiled);

    // The runtime samples every managed thread at this interval, so a method's sample count
    // multiplied by it is the time spent there.
    private const double SampleMilliseconds = 1.0;

    /// <summary>
    /// Runs <paramref name="workload"/> with a trace session open, writes the trace next to the
    /// run's other output, and prints the report the mode asks for.
    /// </summary>
    public static void Collect(ProfileMode mode, string outputDirectory, Action workload)
    {
        string tracePath = Path.Combine(outputDirectory, mode == ProfileMode.Cpu ? "cpu.nettrace" : "memory.nettrace");

        List<EventPipeProvider> providers;

        if (mode == ProfileMode.Cpu)
        {
            providers = new()
            {
                new EventPipeProvider(SampleProfilerTraceEventParser.ProviderName, EventLevel.Informational),
                new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Informational, (long)ClrTraceEventParser.Keywords.Default),
            };
        }
        else
        {
            // The GC keyword at this level is what raises an allocation tick, and the rest is what
            // lets the tick's stack be resolved to method names.
            ClrTraceEventParser.Keywords keywords = ClrTraceEventParser.Keywords.GC
                | ClrTraceEventParser.Keywords.Jit
                | ClrTraceEventParser.Keywords.Loader
                | ClrTraceEventParser.Keywords.NGen;

            providers = new()
            {
                new EventPipeProvider(ClrTraceEventParser.ProviderName, EventLevel.Verbose, (long)keywords),
            };
        }

        DiagnosticsClient client = new(Environment.ProcessId);
        using EventPipeSession session = client.StartEventPipeSession(providers, requestRundown: true, circularBufferMB: CircularBufferMegabytes);

        // The session writes continuously and stalls the run if nothing drains it, so the trace is
        // copied out on its own thread while the workload runs.
        Task copyTask = Task.Run(() =>
        {
            using FileStream traceStream = File.Create(tracePath);
            session.EventStream.CopyTo(traceStream);
        });

        long allocatedBefore = GC.GetTotalAllocatedBytes(precise: true);
        TimeSpan pauseBefore = GC.GetTotalPauseDuration();
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        Stopwatch stopwatch = Stopwatch.StartNew();

        workload();

        stopwatch.Stop();
        long allocatedBytes = GC.GetTotalAllocatedBytes(precise: true) - allocatedBefore;
        TimeSpan pauseTime = GC.GetTotalPauseDuration() - pauseBefore;
        int gen0Count = GC.CollectionCount(0) - gen0Before;
        int gen1Count = GC.CollectionCount(1) - gen1Before;
        int gen2Count = GC.CollectionCount(2) - gen2Before;

        session.Stop();
        copyTask.Wait();

        Console.WriteLine();
        Console.WriteLine($"Workload ran for {stopwatch.Elapsed.TotalSeconds:F2} s; trace written to {tracePath}");

        if (mode == ProfileMode.Cpu)
        {
            PrintCpuReport(tracePath);

            return;
        }

        Console.WriteLine(
            $"Allocated {allocatedBytes / BytesPerMegabyte:F1} MB, collected gen0 {gen0Count} / gen1 {gen1Count} / gen2 {gen2Count}, paused for {pauseTime.TotalMilliseconds:F1} ms");

        PrintMemoryReport(tracePath, allocatedBytes);
    }

    /// <summary>
    /// Dumps the live heap of this process and prints the types holding the most bytes. Called while
    /// the document is still open, so what it reports is what the open document holds on to.
    /// </summary>
    public static void CollectHeapDump(string outputDirectory)
    {
        string dumpPath = Path.Combine(outputDirectory, "heap.gcdump");

        if (File.Exists(dumpPath))
        {
            File.Delete(dumpPath);
        }

        ProcessStartInfo startInfo = new("dotnet")
        {
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("gcdump");
        startInfo.ArgumentList.Add("collect");
        startInfo.ArgumentList.Add("-p");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString());
        startInfo.ArgumentList.Add("-o");
        startInfo.ArgumentList.Add(dumpPath);
        startInfo.ArgumentList.Add("--timeout");
        startInfo.ArgumentList.Add("300");

        using (Process? collector = Process.Start(startInfo))
        {
            if (collector == null)
            {
                Console.WriteLine("Could not start 'dotnet gcdump'; install it with 'dotnet tool install --global dotnet-gcdump'.");

                return;
            }

            collector.WaitForExit();
        }

        if (!File.Exists(dumpPath))
        {
            Console.WriteLine("'dotnet gcdump' produced no dump.");

            return;
        }

        PrintHeapReport(dumpPath);
    }

    /// <summary>
    /// Runs the dump through 'dotnet gcdump report' and totals it by type. The report gives the size of
    /// a single instance and how many of them are alive, so the two are multiplied to get what a type
    /// holds, and the size buckets it splits array types into are folded back together.
    /// </summary>
    private static void PrintHeapReport(string dumpPath)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false
        };

        startInfo.ArgumentList.Add("gcdump");
        startInfo.ArgumentList.Add("report");
        startInfo.ArgumentList.Add(dumpPath);

        using Process? reporter = Process.Start(startInfo);

        if (reporter == null)
        {
            return;
        }

        Dictionary<string, long> bytesByType = new();
        Dictionary<string, long> countByType = new();
        long totalBytes = 0;

        while (reporter.StandardOutput.ReadLine() is string line)
        {
            Match match = HeapReportRow.Match(line);

            if (!match.Success)
            {
                continue;
            }

            long instanceBytes = long.Parse(match.Groups[1].Value.Replace(",", string.Empty));
            long instanceCount = long.Parse(match.Groups[2].Value.Replace(",", string.Empty));
            string typeName = SizeBucketSuffix.Replace(match.Groups[3].Value, string.Empty).Trim();

            Add(bytesByType, typeName, instanceBytes * instanceCount);
            Add(countByType, typeName, instanceCount);
            totalBytes += instanceBytes * instanceCount;
        }

        reporter.WaitForExit();

        if (totalBytes == 0)
        {
            Console.WriteLine("The heap report named no types.");

            return;
        }

        Console.WriteLine();
        Console.WriteLine($"Live heap holds {totalBytes / BytesPerMegabyte:F1} MB; dump written to {dumpPath}");
        Console.WriteLine();
        Console.WriteLine("Retained by type");

        foreach (KeyValuePair<string, long> entry in bytesByType.OrderByDescending(static entry => entry.Value).Take(TopCount))
        {
            Console.WriteLine($"{entry.Value / BytesPerMegabyte,10:F1} MB {entry.Value * 100.0 / totalBytes,6:F1}% {countByType[entry.Key],12:N0}  {entry.Key}");
        }
    }

    /// <summary>
    /// Prints the methods the sampled stacks were caught in: first the ones running themselves,
    /// then the ones that had a call in flight.
    /// </summary>
    private static void PrintCpuReport(string tracePath)
    {
        Dictionary<string, int> exclusiveSamples = new();
        Dictionary<string, int> inclusiveSamples = new();
        List<string> frameNames = new();
        int totalSamples = 0;
        int renderingSamples = 0;

        using TraceLog traceLog = OpenTraceLog(tracePath);

        foreach (TraceEvent traceEvent in traceLog.Events)
        {
            if (traceEvent.ProviderName != SampleProfilerTraceEventParser.ProviderName)
            {
                continue;
            }

            TraceCallStack? stack = traceEvent.CallStack();

            if (stack == null)
            {
                continue;
            }

            totalSamples++;
            frameNames.Clear();
            bool rendersPage = false;

            for (TraceCallStack? frame = stack; frame != null; frame = frame.Caller)
            {
                frameNames.Add(DescribeFrame(frame));

                if (IsRenderingFrame(frame))
                {
                    rendersPage = true;
                }
            }

            // The run has a thread draining the trace, a thread printing the log, and a pool of
            // threads parked on a wait, and every one of them is sampled as often as the thread
            // doing the work. Only the stacks that reach the renderer say anything about the page.
            if (!rendersPage)
            {
                continue;
            }

            renderingSamples++;
            Add(exclusiveSamples, frameNames[0], 1);

            // A method that appears twice in one stack, through recursion, still had a single call
            // in flight for this sample.
            HashSet<string> countedFrames = new();

            foreach (string frameName in frameNames)
            {
                if (countedFrames.Add(frameName))
                {
                    Add(inclusiveSamples, frameName, 1);
                }
            }
        }

        if (renderingSamples == 0)
        {
            Console.WriteLine($"None of the {totalSamples} sample(s) carried a stack that reached the renderer.");

            return;
        }

        Console.WriteLine($"{renderingSamples} of {totalSamples} sample(s) ran rendering code, {SampleMilliseconds:F0} ms each; native frames are reported by module.");
        PrintSampleTable("Running in the method itself", exclusiveSamples, renderingSamples);
        PrintSampleTable("Somewhere on the stack", inclusiveSamples, renderingSamples);
    }

    /// <summary>
    /// Prints what the run put on the managed heap: the types allocated, and the methods that
    /// allocated them. The runtime raises a tick every time a fixed amount has been allocated and
    /// names the type that crossed the line, so each tick stands for an equal share of
    /// <paramref name="allocatedBytes"/>.
    /// </summary>
    private static void PrintMemoryReport(string tracePath, long allocatedBytes)
    {
        Dictionary<string, int> ticksByType = new();
        Dictionary<string, int> ticksByMethod = new();
        int totalTicks = 0;

        using TraceLog traceLog = OpenTraceLog(tracePath);

        foreach (TraceEvent traceEvent in traceLog.Events)
        {
            if (traceEvent is not GCAllocationTickTraceData allocation)
            {
                continue;
            }

            totalTicks++;
            Add(ticksByType, allocation.TypeName, 1);

            TraceCallStack? stack = allocation.CallStack();

            if (stack != null)
            {
                Add(ticksByMethod, DescribeFrame(stack), 1);
            }
        }

        if (totalTicks == 0)
        {
            Console.WriteLine("The run did not allocate enough for the runtime to raise a single allocation tick.");

            return;
        }

        double bytesPerTick = allocatedBytes / (double)totalTicks;
        Console.WriteLine($"{totalTicks} allocation tick(s), {bytesPerTick / 1024:F0} KB each; the split is sampled, the total is exact.");
        PrintByteTable("Allocated type", ticksByType, totalTicks, allocatedBytes);
        PrintByteTable("Allocated from", ticksByMethod, totalTicks, allocatedBytes);
    }

    private static TraceLog OpenTraceLog(string tracePath)
    {
        // Stack resolution needs the converted form of the trace, which is written next to it.
        string convertedPath = TraceLog.CreateFromEventPipeDataFile(tracePath);

        return new TraceLog(convertedPath);
    }

    private static void PrintSampleTable(string title, Dictionary<string, int> samples, int totalSamples)
    {
        Console.WriteLine();
        Console.WriteLine(title);

        foreach (KeyValuePair<string, int> entry in samples.OrderByDescending(static entry => entry.Value).Take(TopCount))
        {
            Console.WriteLine($"{entry.Value * SampleMilliseconds,10:F0} ms {entry.Value * 100.0 / totalSamples,6:F1}%  {entry.Key}");
        }
    }

    private static void PrintByteTable(string title, Dictionary<string, int> ticks, int totalTicks, long allocatedBytes)
    {
        Console.WriteLine();
        Console.WriteLine(title);

        foreach (KeyValuePair<string, int> entry in ticks.OrderByDescending(static entry => entry.Value).Take(TopCount))
        {
            double share = entry.Value / (double)totalTicks;
            Console.WriteLine($"{share * allocatedBytes / BytesPerMegabyte,10:F1} MB {share * 100.0,6:F1}%  {entry.Key}");
        }
    }

    private static void Add<TValue>(Dictionary<string, TValue> totals, string key, TValue value)
        where TValue : struct, System.Numerics.IAdditionOperators<TValue, TValue, TValue>
    {
        if (totals.TryGetValue(key, out TValue existing))
        {
            totals[key] = existing + value;
        }
        else
        {
            totals[key] = value;
        }
    }

    // This tool's own assembly drives the run and appears on every thread it starts, so a stack
    // counts as rendering work only once it reaches one of the libraries under test.
    private static bool IsRenderingFrame(TraceCallStack frame)
    {
        string moduleName = frame.CodeAddress.ModuleName;

        return moduleName.StartsWith("PdfPixel", StringComparison.Ordinal)
            && !moduleName.Equals("PdfPixel.Diagnostics", StringComparison.Ordinal);
    }

    // A frame whose method the runtime never reported - native code, or a stack walked past the
    // managed part - is named after the module it sits in.
    private static string DescribeFrame(TraceCallStack frame)
    {
        TraceMethod? method = frame.CodeAddress.Method;

        if (method != null)
        {
            return method.FullMethodName;
        }

        string moduleName = frame.CodeAddress.ModuleName;

        return string.IsNullOrEmpty(moduleName) ? "unresolved" : $"{moduleName}!native";
    }
}
