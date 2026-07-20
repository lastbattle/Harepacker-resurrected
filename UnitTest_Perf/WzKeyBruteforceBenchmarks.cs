using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Order;
using BenchmarkDotNet.Running;
using HaRepacker.GUI;
using MapleLib.WzLib;
using MapleLib.WzLib.Util;
using System.Buffers.Binary;

namespace UnitTest_Perf;

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class WzKeyBruteforceBenchmarks
{
    private const int CandidatesPerInvocation = 16;
    private string _wzPath = null!;
    private WzKeyBruteforceProbe _probe = null!;
    private WzKeyBruteforceProbe.Worker _worker = null!;

    [GlobalSetup]
    public void Setup()
    {
        _wzPath = Environment.GetEnvironmentVariable("WZ_KEY_BENCHMARK_FILE")
            ?? throw new InvalidOperationException("Set WZ_KEY_BENCHMARK_FILE to a live TamingMob.wz file.");

        if (!File.Exists(_wzPath))
            throw new FileNotFoundException("The live WZ benchmark file was not found.", _wzPath);

        _probe = new WzKeyBruteforceProbe(_wzPath);
        _worker = _probe.CreateWorker();
    }

    [GlobalCleanup]
    public void Cleanup() => _worker.Dispose();

    [Benchmark(Baseline = true, OperationsPerInvoke = CandidatesPerInvocation)]
    public int ExistingFileParsePerCandidate()
    {
        int matches = 0;
        for (int i = 0; i < CandidatesPerInvocation; i++)
        {
            byte[] candidate = BitConverter.GetBytes(unchecked((int)(0x10203040u + (uint)i)));
            matches += WzTool.TryBruteforcingWzIVKey(_wzPath, candidate) ? 1 : 0;
        }

        return matches;
    }

    [Benchmark(OperationsPerInvoke = 1024)]
    public int BatchedEncryptedNameProbe1024()
    {
        return _worker.CountSignatureMatches(0x10203040u, 1024);
    }

    [Benchmark(OperationsPerInvoke = 4096)]
    public int BatchedEncryptedNameProbe4096()
    {
        return _worker.CountSignatureMatches(0x10203040u, 4096);
    }

    [Benchmark(OperationsPerInvoke = 8192)]
    public int BatchedEncryptedNameProbe8192()
    {
        return _worker.CountSignatureMatches(0x10203040u, 8192);
    }

    [Benchmark(OperationsPerInvoke = 16_384)]
    public int BatchedEncryptedNameProbe16384()
    {
        return _worker.CountSignatureMatches(0x10203040u, 16_384);
    }

}

[MemoryDiagnoser]
[Orderer(SummaryOrderPolicy.FastestToSlowest)]
[SimpleJob(warmupCount: 2, iterationCount: 8)]
public class WzKeyBruteforceParallelBenchmarks
{
    private const int TotalCandidates = 4_194_240;
    private const int MaximumWorkers = 48;

    private WzKeyBruteforceProbe _probe = null!;
    private WzKeyBruteforceProbe.Worker[] _workers = null!;

    [Params(1, 8, 16, 32, 48)]
    public int WorkerCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        string wzPath = Environment.GetEnvironmentVariable("WZ_KEY_BENCHMARK_FILE")
            ?? throw new InvalidOperationException("Set WZ_KEY_BENCHMARK_FILE to a live TamingMob.wz file.");
        _probe = new WzKeyBruteforceProbe(wzPath);
        _workers = Enumerable.Range(0, MaximumWorkers).Select(_ => _probe.CreateWorker()).ToArray();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        foreach (WzKeyBruteforceProbe.Worker worker in _workers)
            worker.Dispose();
    }

    [Benchmark(OperationsPerInvoke = TotalCandidates)]
    public int ParallelBatchedProbe()
    {
        int matches = 0;
        Parallel.For(
            0,
            WorkerCount,
            new ParallelOptions { MaxDegreeOfParallelism = WorkerCount },
            workerId =>
            {
                int localMatches = 0;
                ulong rangeStart = (ulong)TotalCandidates * (ulong)workerId / (ulong)WorkerCount;
                ulong rangeEnd = (ulong)TotalCandidates * (ulong)(workerId + 1) / (ulong)WorkerCount;
                ulong current = rangeStart;

                while (current < rangeEnd)
                {
                    int count = (int)Math.Min(
                        (ulong)WzKeyBruteforceProbe.CandidateBatchSize,
                        rangeEnd - current);
                    localMatches += _workers[workerId].CountSignatureMatches(
                        unchecked(0x10203040u + (uint)current),
                        count);
                    current += (uint)count;
                }

                Interlocked.Add(ref matches, localMatches);
            });

        return matches;
    }
}

[TestClass]
public class WzKeyBruteforceBenchmarkTests
{
    [TestMethod]
    public void RunLiveBenchmark()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_WZ_KEY_BENCHMARK"), "1", StringComparison.Ordinal))
            return;

        BenchmarkRunner.Run<WzKeyBruteforceBenchmarks>();
    }

    [TestMethod]
    public void RunLiveParallelBenchmark()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_WZ_KEY_PARALLEL_BENCHMARK"), "1", StringComparison.Ordinal))
            return;

        BenchmarkRunner.Run<WzKeyBruteforceParallelBenchmarks>();
    }

    [TestMethod]
    public void FindsExpectedKeyInLiveFile()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable("RUN_WZ_KEY_CORRECTNESS"), "1", StringComparison.Ordinal))
            return;

        string wzPath = Environment.GetEnvironmentVariable("WZ_KEY_BENCHMARK_FILE")
            ?? throw new InvalidOperationException("Set WZ_KEY_BENCHMARK_FILE to a live TamingMob.wz file.");
        string expectedHex = Environment.GetEnvironmentVariable("WZ_KEY_EXPECTED_HEX")
            ?? throw new InvalidOperationException("Set WZ_KEY_EXPECTED_HEX to the expected four key bytes.");

        byte[] expectedBytes = Convert.FromHexString(expectedHex);
        Assert.AreEqual(4, expectedBytes.Length);
        uint expected = BinaryPrimitives.ReadUInt32LittleEndian(expectedBytes);

        WzKeyBruteforceProbe probe = new WzKeyBruteforceProbe(wzPath);
        Assert.IsTrue(probe.TryCandidate(expected), "The fast signature and full WZ parser should accept the expected key.");
        Assert.IsFalse(probe.TryCandidate(0x10203040u), "An unrelated candidate should be rejected.");

        using WzKeyBruteforceProbe.Worker worker = probe.CreateWorker();
        ulong rangeStart = expected == 0 ? 0 : expected - 1UL;
        ulong rangeEnd = (ulong)expected + 2UL;
        uint? found = worker.FindFirst(
            rangeStart,
            rangeEnd,
            CancellationToken.None,
            () => false,
            _ => { });

        Assert.AreEqual(expected, found);
    }
}
