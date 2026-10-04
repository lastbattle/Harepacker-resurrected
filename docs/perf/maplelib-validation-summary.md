# MapleLib performance validation summary

## Correctness checks

- MapleLib.Tests: 524 passed.
- Independent image and DXT checks: 16 passed in the final image-format run.
- Serialization, packet, crypto, stream-position, cache, and wire-byte checks were run alongside the retained iterations.

## Live corpus checks

| Corpus | Files checked | Round-trip samples | Mismatches | Interpretation |
|---|---:|---:|---:|---|
| GMS v95 | 23,048 | 180 | 0 | Full structural and representative round-trip validation passed. |
| GMS v188 Tespia | 69,243 | 208 | 2 | Two baseline-reproduced sound metadata differences; byte payloads matched. |

The `208` value for GMS v188 Tespia is the number of sampled round trips. It is not a failure count; only two samples differed, and both differences matched the original baseline's sound metadata behavior.

The corpus checks cover structural loading, cache/reload behavior, selected serializer/media round trips, and link handling. They are compatibility receipts rather than a claim that every payload was fully re-encoded.

## Measurement policy

Benchmarks were run serially to avoid CPU contention. Each retained change had a matched original control, repeated measurements, and an independent correctness check. Candidates that regressed larger inputs, changed public semantics, or fell inside measurement variance were restored and excluded from the optimization summary.

The source campaign notes, raw BenchmarkDotNet output, temporary probes, generated binaries, and local corpus paths were disposable and have been removed. This summary intentionally records only portable results and known limitations.
