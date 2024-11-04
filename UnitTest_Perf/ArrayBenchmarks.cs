using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_Perf
{
    [MemoryDiagnoser]
    [RankColumn]
    [Orderer(SummaryOrderPolicy.FastestToSlowest)]
    [IterationCount(5)]
    [WarmupCount(1)]
    [SimpleJob()]
    public class ArrayBenchmarks
    {

        private const int TINY_SIZE = 256;          // 256 bytes
        private const int SMALLS_ZIE = 1024;        // 1 KB
        private const int MEDIUM_SIZE = 1024 * 1024; // 1 MB
        private const int LARGE_SIZE = 10 * 1024 * 1024; // 10 MB

        // Simulate real workload to prevent dead code elimination
        private byte _dummyValue;

        [GlobalSetup]
        public void Setup()
        {
            _dummyValue = 42;
        }

        [Benchmark(Description = "Stackalloc 256B")]
        public byte TinyArray_Stackalloc()
        {
            Span<byte> array = stackalloc byte[TINY_SIZE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % TINY_SIZE]; // Prevent optimization
        }

        [Benchmark(Description = "Unsafe Stackalloc 256B")]
        public unsafe byte TinyArray_StackallocUnsafe()
        {
            byte* array = stackalloc byte[TINY_SIZE];
            for (int i = 0; i < TINY_SIZE; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % TINY_SIZE];
        }

        [Benchmark(Description = "byte[] 256B")]
        public byte TinyArray_DirectAllocation()
        {
            byte[] array = new byte[TINY_SIZE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % TINY_SIZE];
        }

        [Benchmark(Description = "ArrayPool 256B")]
        public byte TinyArray_ArrayPool()
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(TINY_SIZE);
            try
            {
                for (int i = 0; i < TINY_SIZE; i++)
                {
                    array[i] = (byte)(i % 256);
                }
                return array[_dummyValue % TINY_SIZE];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        [Benchmark(Description = "Stackalloc 1KB")]
        public byte SmallArray_Stackalloc()
        {
            Span<byte> array = stackalloc byte[SMALLS_ZIE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % SMALLS_ZIE];
        }

        [Benchmark(Description = "byte[] 1KB")]
        public byte SmallArray_DirectAllocation()
        {
            byte[] array = new byte[SMALLS_ZIE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % SMALLS_ZIE];
        }

        [Benchmark(Description = "ArrayPool 1KB")]
        public byte SmallArray_ArrayPool()
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(SMALLS_ZIE);
            try
            {
                for (int i = 0; i < SMALLS_ZIE; i++)
                {
                    array[i] = (byte)(i % 256);
                }
                return array[_dummyValue % SMALLS_ZIE];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        [Benchmark(Description = "byte[] 1MB")]
        public byte MediumArray_DirectAllocation()
        {
            byte[] array = new byte[MEDIUM_SIZE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % MEDIUM_SIZE];
        }

        [Benchmark(Description = "ArrayPool 1MB")]
        public byte MediumArray_ArrayPool()
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(MEDIUM_SIZE);
            try
            {
                for (int i = 0; i < MEDIUM_SIZE; i++)
                {
                    array[i] = (byte)(i % 256);
                }
                return array[_dummyValue % MEDIUM_SIZE];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        [Benchmark(Description = "byte[] 10MB")]
        public byte LargeArray_DirectAllocation()
        {
            byte[] array = new byte[LARGE_SIZE];
            for (int i = 0; i < array.Length; i++)
            {
                array[i] = (byte)(i % 256);
            }
            return array[_dummyValue % LARGE_SIZE];
        }

        [Benchmark(Description = "ArrayPool 10MB")]
        public byte LargeArray_ArrayPool()
        {
            byte[] array = ArrayPool<byte>.Shared.Rent(LARGE_SIZE);
            try
            {
                for (int i = 0; i < LARGE_SIZE; i++)
                {
                    array[i] = (byte)(i % 256);
                }
                return array[_dummyValue % LARGE_SIZE];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }

        [Benchmark(Description = "Real-World Mixed")]
        public byte RealWorld_DataProcessing()
        {
            Span<byte> tempBuffer = stackalloc byte[256];
            byte[] processingBuffer = ArrayPool<byte>.Shared.Rent(MEDIUM_SIZE);
            try
            {
                for (int i = 0; i < 256; i++)
                {
                    tempBuffer[i] = (byte)(i % 256);
                }

                for (int i = 0; i < MEDIUM_SIZE; i++)
                {
                    processingBuffer[i] = (byte)(tempBuffer[i % 256] * 2);
                }

                return processingBuffer[_dummyValue % MEDIUM_SIZE];
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(processingBuffer);
            }
        }
    }
}