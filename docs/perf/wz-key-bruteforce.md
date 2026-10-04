# WZ Key Bruteforce Benchmark Report

## Scope and compatibility contract

- Target: `HaRepacker/GUI/WzKeyBruteforceForm.cs` and its 32-bit WZ IV search.
- Preserve the four-byte key representation, full 0 through `uint.MaxValue` search space, cancellation, progress count, and final `WzTool.TryBruteforcingWzIVKey` acceptance behavior.
- Internal implementation changes were allowed. No public API or file-format behavior changed.
- The fast path requires a classic WZ root image name whose encrypted `.img` suffix is inside the first 16 key-stream bytes. The form already recommends `TamingMob.wz`; unsupported files now receive a specific error instead of beginning an impractically slow search.

## Benchmark setup

- OS: Windows desktop environment.
- Runtime: .NET 10, Release `net10.0-windows`.
- BenchmarkDotNet 0.15.6, two warmups and eight measured iterations.
- Primary live workload: Taiwan v113 `TamingMob.wz` (899 bytes).
- Metric: unsuccessful candidates/second. This avoids the misleading near-zero duration of common zero/GMS/MSEA keys and measures the exhaustive-search work users wait for.
- Primary command: set `RUN_WZ_KEY_BENCHMARK=1` and `WZ_KEY_BENCHMARK_FILE` to the live file, then run `dotnet test UnitTest_Perf/UnitTest_Perf.csproj -c Release --filter FullyQualifiedName~WzKeyBruteforceBenchmarkTests.RunLiveBenchmark`.
- Parallel command: use `RUN_WZ_KEY_PARALLEL_BENCHMARK=1` and filter `RunLiveParallelBenchmark`.

## Final result

- Same-run single-worker baseline: 239,282.059 ns and 17,664 allocated bytes per candidate.
- Retained implementation: 2.578 ns and zero steady-state allocated bytes per candidate: 92,817x faster and about 387.9 million candidates/second per worker benchmark.
- Final 48-worker measurement: 0.3415 ns/candidate aggregate, about 2.93 billion candidates/second. This implies roughly 1.47 seconds for the signature-filter portion of the complete 32-bit space in the benchmark environment; scheduling and the rare final full parse add overhead.
- Each worker owns two 256 KiB buffers at the retained 16K batch size. Forty-eight workers therefore use about 24 MiB of reusable buffers instead of allocating 17.25 KiB for every candidate.
- `Aes.Create()` supplies the runtime/platform accelerated implementation. Explicit x86 AES intrinsics were not added because batching already reached the plateau and the project also targets x86 and ARM64.

## Correctness checks

- Taiwan v113 `TamingMob.wz`: `B9 7D 63 E9`.
- MapleStorySEA v82 `TamingMob.wz`: `B9 7D 63 E9`.
- Global v95 Ariku `TamingMob.wz`: `4D 23 C7 2B`.
- Global v146 `TamingMob.wz`: `00 00 00 00`.
- Every live check validates both the fast suffix filter and the existing full WZ parser, rejects an unrelated key, and finds the expected key inside a small searched range.

## Iterations

| Iteration | Label | Metrics | Decision | Change | Notes |
| --- | --- | --- | --- | --- | --- |
| 0 | existing parser baseline | mean_us_per_candidate=236.599,stddev_us=3.142,throughput_candidates_per_s=4226,allocated_kb_per_candidate=17.25 | baseline | Added gated BenchmarkDotNet live-file harness; production code unchanged | TMS v113 `TamingMob.wz`, 899 bytes; Release `net10.0-windows`; 2 warmups, 8 iterations, 16 candidates/invocation |
| 1 | preloaded batched AES suffix probe | mean_ns_per_candidate=2.599,stddev_ns=0.050,throughput_candidates_per_s=384763371,allocated_bytes_per_candidate=0,speedup_vs_same_run_baseline=84878x | keep | Preload encrypted `.img` suffix, reuse AES worker, transform 4096 IVs per batch, full-parse signature hits only; check zero/GMS/MSEA first | Live correctness passed for Taiwan v113, MSEA v82, GMS v95 Ariku, and zero-key GMS v146; same-run baseline 220598.006 ns and 17664 B/candidate |
| 2 | worker-count scaling | 1_worker_ns=3.4744,8_workers_ns=0.4698,16_workers_ns=0.3766,32_workers_ns=0.3474,48_workers_ns=0.3147,48_worker_throughput_candidates_per_s=3177629488 | keep | Use `Environment.ProcessorCount * 3` batched workers | 48 workers improved aggregate throughput 19.7% over 16 workers; zero managed allocation; 4,194,240 live candidates/invocation |
| 3 | AES batch-size sweep | 1024_batch_ns=2.604,4096_batch_ns=2.595,8192_batch_ns=2.591,16384_batch_ns=2.510,16384_throughput_candidates_per_s=398406375 | keep | Increase worker AES batch from 4096 to 16384 candidates | 16K was 3.3% faster than 4K; zero steady-state allocation; same-run parser baseline 220860.315 ns/candidate |
| 4 | large AES batches | 16384_batch_ns=2.502,32768_batch_ns=2.528,65536_batch_ns=2.532 | revert | Tested 32K and 64K worker batches; restored 16K | 32K and 64K regressed 1.0% and 1.2% while doubling/quadrupling per-worker buffer memory |
| 5 | packed suffix comparison | packed_16384_batch_ns=2.714,array_loop_prior_ns=2.502,regression_percent=8.5 | revert | Tested packed 32/64-bit suffix signature with aggressive inlining; restored constraint loop | Packed comparison regressed the primary 16K path and changed the apparent best batch to 8K |
| 6 | final unchanged plateau rerun | mean_ns_per_candidate=2.578,stddev_ns=0.0276,throughput_candidates_per_s=387897595,same_run_baseline_ns=239282.059,speedup=92817x,allocated_bytes_per_candidate=0 | stop | No production change; confirmed restored 16K batch and array suffix check | Third consecutive plateau/regression measurement; stop threshold reached |
| 7 | final 16K parallel validation | 1_worker_ns=2.7995,8_workers_ns=0.5513,16_workers_ns=0.3841,32_workers_ns=0.3467,48_workers_ns=0.3415,48_worker_throughput_candidates_per_s=2928257687 | stop | No production change; retained `logical worker count * 3` policy | 48 workers remained fastest, 1.5% ahead of 32 workers but confidence intervals overlap; full parser still verifies hits |
