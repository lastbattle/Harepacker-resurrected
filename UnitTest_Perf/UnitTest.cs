using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace UnitTest_Perf
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public void StartBenchmark()
        {
            // Run all benchmarks in a class
            var debugConfig = new DebugInProcessConfig();
            BenchmarkRunner.Run<ArrayBenchmarks>(debugConfig);
        }
    }
}
