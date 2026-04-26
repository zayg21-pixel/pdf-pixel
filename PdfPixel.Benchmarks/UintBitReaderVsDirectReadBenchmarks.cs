using BenchmarkDotNet.Attributes;
using PdfPixel.Parsing;
using System;

namespace Benchmarks
{
    [MemoryDiagnoser]
    public class UintBitReaderVsDirectReadBenchmarks
    {
        private byte[] _data;
        private const int N = 10_000_000;
        private uint[] _values;

        [GlobalSetup]
        public void Setup()
        {
            _data = new byte[N];
            _values = new uint[N];
            var rand = new Random(123);
            rand.NextBytes(_data);
        }

        [Benchmark(Description = "UintBitReader Read 8 bits")]
        public void UintBitReader_Read8Bits()
        {
            var reader = new UintBitReader(_data);
            for (int i = 0; i < N; i++)
            {
                _values[i] = reader.ReadBits(8);
            }
        }


        [Benchmark(Description = "UintBitReaderPowerOf2FixedLength Read 8 bits")]
        public void UintBitReaderPowerOf2F_Read8Bits()
        {
            var reader = new UintBitReaderFixedLength(_data, 8);
            for (int i = 0; i < N; i++)
            {
                _values[i] = reader.Read();
            }
        }

        [Benchmark(Description = "Direct Read from Array as uint")]
        public void DirectRead_Array()
        {
            for (int i = 0; i < N; i++)
            {
                _values[i] = _data[i];
            }
        }

        public void Verivy()
        {
            var reader0 = new UintBitReaderFixedLength(_data, 16);
            reader0.Read();
            int cnt = 32;

            for (int i = 1; i < 32; i++)
            {
                var reader = new UintBitReaderFixedLength(_data, cnt);
                var v1 = reader.Read();
                var v2 = reader.Read();

                var reader2 = new UintBitReader(_data);
                var v3 = reader2.ReadBits(cnt);
                var v4 = reader2.ReadBits(cnt);

                if (v1 != v3 || v2 != v4)
                {
                    throw new InvalidOperationException();
                }

            }
        }
    }
}
