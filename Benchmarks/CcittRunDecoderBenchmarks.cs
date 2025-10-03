using System;
using BenchmarkDotNet.Attributes;
using PdfReader.Rendering.Image.Ccitt;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class CcittRunDecoderBenchmarks
    {
        private byte[] _whiteSample;
        private byte[] _blackSample;

        [GlobalSetup]
        public void Setup()
        {
            // Sample bitstreams: alternating short and long runs, valid for both tables
            _whiteSample = new byte[] { 0b00110101, 0b01100100, 0b00011100, 0b11010000 };
            _blackSample = new byte[] { 0b00001101, 0b11000000, 0b11110100, 0b00001100 };
        }

        [Benchmark]
        public void DecodeWhiteCodes()
        {
            var reader = new CcittBitReader(_whiteSample);
            for (int i = 0; i < 1000; i++)
            {
                var code = CcittRunDecoder.DecodeSingleCode(ref reader, CcittCodeTables.WhiteLookup);
                if (code == null)
                {
                    break;
                }
            }
        }

        [Benchmark]
        public void DecodeBlackCodes()
        {
            var reader = new CcittBitReader(_blackSample);
            for (int i = 0; i < 1000; i++)
            {
                var code = CcittRunDecoder.DecodeSingleCode(ref reader, CcittCodeTables.BlackLookup);
                if (code == null)
                {
                    break;
                }
            }
        }
    }
}
