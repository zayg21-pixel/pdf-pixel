using System;
using BenchmarkDotNet.Attributes;
using PdfReader.Rendering.Image.Jpg.Idct;
using Microsoft.VSDiagnostics;
using System.Runtime.CompilerServices;
using PdfReader.Rendering.Image.Jpg.Decoding;

namespace Benchmarks
{
    [CPUUsageDiagnoser]
    public class JpgIdctTransformBench
    {
        private const int BlockSize = 64;
        private const int OutStride = 8;

        private int[] inputCoefficientsZigZag; // Coefficients in zig-zag order (flat)
        private ScaledIdctPlan plan;

        // Array implementation work buffers
        private byte[] outputArray;               // Output buffer (array based transform)
        private byte[] outputBlock;               // Output buffer (Block8x8 transform)
        private int[] workspace;                  // Workspace (natural order) for array version
        private int[] subWorkspace;               // Temp (zig-zag dequant) for array version
        [GlobalSetup]
        public void Setup()
        {
            var random = new Random(42);

            // Typical (simplified) luminance quantization table (zig-zag order) for demonstration.
            int[] quantTableZigZag = new int[BlockSize]
            {
                16,11,10,16,24,40,51,61,
                12,12,14,19,26,58,60,55,
                14,13,16,24,40,57,69,56,
                14,17,22,29,51,87,80,62,
                18,22,37,56,68,109,103,77,
                24,35,55,64,81,104,113,92,
                49,64,78,87,103,121,120,101,
                72,92,95,98,112,100,103,99
            };

            // Build natural-order quantization table using zig-zag map.
            int[] quantTableNatural = new int[BlockSize];
            for (int i = 0; i < BlockSize; i++)
            {
                int naturalIndex = JpgZigZag.Table[i];
                quantTableNatural[naturalIndex] = quantTableZigZag[i];
            }

            plan = new ScaledIdctPlan(0, quantTableZigZag, quantTableNatural);

            inputCoefficientsZigZag = new int[BlockSize];
            for (int i = 0; i < BlockSize; i++)
            {
                // DC magnitude larger, AC smaller / signed
                inputCoefficientsZigZag[i] = (i == 0) ? random.Next(0, 1024) : random.Next(-30, 31);
            }

            // Build natural-order block from zig-zag coefficients for Block8x8 path.
            for (int zigZagIndex = 0; zigZagIndex < BlockSize; zigZagIndex++)
            {
                int naturalIndex = JpgZigZag.Table[zigZagIndex];
                int rowIndex = naturalIndex / 8;
                int columnIndex = naturalIndex % 8;
            }

            outputArray = new byte[BlockSize];
            outputBlock = new byte[BlockSize];
            workspace = new int[BlockSize];
            subWorkspace = new int[BlockSize];
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private int HashOutput(byte[] buffer)
        {
            int hash = 17;
            for (int i = 0; i < buffer.Length; i++)
            {
                hash = (hash * 31) + buffer[i];
            }
            return hash;
        }

        [Benchmark(Baseline = true)]
        public int TransformScaledZigZag_Array()
        {
            JpgIdct.TransformScaledZigZag(inputCoefficientsZigZag, plan, outputArray, OutStride, workspace, subWorkspace);
            return HashOutput(outputArray);
        }
    }
}