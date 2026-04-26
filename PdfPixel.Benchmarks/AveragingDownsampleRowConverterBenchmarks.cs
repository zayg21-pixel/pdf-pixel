using BenchmarkDotNet.Attributes;
using PdfPixel.Imaging.Processing;
using System;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class AveragingDownsampleRowConverterBenchmarks
    {
        private const int SourceWidth = 4000;
        private const int SourceHeight = 4000;
        private const int Components = 3;
        private const int BitsPerComponent = 8;
        private const int DestWidth = SourceWidth / 5;
        private const int DestHeight = SourceHeight / 5;
        private byte[][] _sourceRows;
        private byte[][] _destRows;

        [GlobalSetup]
        public void Setup()
        {
            var rand = new Random(42);
            _sourceRows = new byte[SourceHeight][];
            for (int i = 0; i < SourceHeight; i++)
            {
                _sourceRows[i] = new byte[(SourceWidth * Components * BitsPerComponent + 7) / 8];
                rand.NextBytes(_sourceRows[i]);
            }
            _destRows = new byte[DestHeight][];
            for (int i = 0; i < DestHeight; i++)
            {
                _destRows[i] = new byte[(DestWidth * Components * BitsPerComponent + 7) / 8];
            }
        }

        [Benchmark(Description = "Nearest Neighbor")]
        public void NearestNeighborRowConverter_Benchmark()
        {
            var converter = new NearestNeighborRowConverter(Components, BitsPerComponent, SourceWidth, DestWidth, SourceHeight, DestHeight);
            int destRow = 0;
            for (int srcRow = 0; srcRow < SourceHeight; srcRow++)
            {
                if (destRow < DestHeight && converter.TryConvertRow(srcRow, _sourceRows[srcRow], _destRows[destRow]))
                {
                    destRow++;
                }
            }
        }

        [Benchmark(Description = "Current (range loop)")]
        public void AveragingDownsampleRowConverter_Benchmark()
        {
            var converter = new AveragingDownsampleRowConverter(Components, BitsPerComponent, SourceWidth, DestWidth, SourceHeight, DestHeight);
            int destRow = 0;
            for (int srcRow = 0; srcRow < SourceHeight; srcRow++)
            {
                if (destRow < DestHeight && converter.TryConvertRow(srcRow, _sourceRows[srcRow], _destRows[destRow]))
                {
                    destRow++;
                }
            }
        }
    }
}
