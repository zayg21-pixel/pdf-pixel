using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnosers;
using PdfReader.Color.Icc.Model;
using PdfReader.Color.Transform;

namespace Benchmarks;

[DisassemblyDiagnoser(printSource: true, maxDepth: 3)]
public class PerChannelTrcTransformBenchmarks
{
    private readonly PerChannelTrcTransform _gammaTransform;
    private readonly PerChannelTrcTransform _sampledTransform;
    private readonly Vector4 _input;

    public PerChannelTrcTransformBenchmarks()
    {
        // Use a gamma curve (e.g., sRGB gamma ~2.2)
        var trc = IccTrc.FromGamma(2.2f);
        _gammaTransform = new PerChannelTrcTransform(trc, trc, trc);
        _input = new Vector4(0.5f, 0.5f, 0.5f, 1.0f);

        // Create a sampled TRC with 1024 items (linear ramp 0..1)
        float[] samples = new float[1024];
        for (int i = 0; i < samples.Length; i++)
        {
            samples[i] = i / (samples.Length - 1.0f);
        }
        var sampledTrc = IccTrc.FromSamples(samples);
        _sampledTransform = new PerChannelTrcTransform(sampledTrc, sampledTrc, sampledTrc);
    }

    [Benchmark]
    public Vector4 TransformGamma()
    {
        return _gammaTransform.Transform(_input);
    }

    [Benchmark]
    public Vector4 TransformSampled1024()
    {
        return _sampledTransform.Transform(_input);
    }
}
