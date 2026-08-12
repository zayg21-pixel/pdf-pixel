using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing;
using Microsoft.Diagnostics.Tracing.Etlx;
using Microsoft.Diagnostics.Tracing.EventPipe;
using Microsoft.Diagnostics.Tracing.Parsers;
using Microsoft.Diagnostics.Tracing.Parsers.Clr;
using System.Diagnostics;
using System.Diagnostics.Tracing;

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
