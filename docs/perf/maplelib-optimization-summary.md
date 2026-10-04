# MapleLib performance optimization summary

This document aggregates the retained, behavior-preserving MapleLib optimizations from the serialized benchmark campaign. Measurements are Release BenchmarkDotNet runs with serialized CPU execution; values are representative before → after means from the campaign ledger. Rejected or inconclusive experiments are omitted.

| Area / function | Change | Measured result |
|---|---|---|
| `WzBinaryReader.ReadNullTerminatedString` | Pooled seekable-stream chunks and span terminator scans | 4 KiB read about 54% faster; allocation unchanged. Small inputs remained within timing noise. |
| `WzImage.CalculateAndSetImageChecksum` | Reused a pooled 64 KiB buffer | 4 KiB: 1.320 → 0.817 µs and 65,560 → 0 B; 64 KiB: 13.019 → 12.481 µs and 65,560 → 0 B. |
| `VirtualWzDirectory` lookups | Indexed loops and ordinal/culture-aware comparisons without lowercase or LINQ allocations | Live v95 lookup: 46.66 → 10.89 ns and 240 → 0 B (about 77% faster). Ordinal lookup microbenchmark: 39.062 ns / 176 B → 3.069 ns / 0 B. |
| Packet string materialization | Stack-backed buffers for short strings | 128 bytes: 18.887 → 15.972 ns and 432 → 280 B; 256 bytes: 25.643 → 20.649 ns and 816 → 536 B. |
| `WzSerializer.WritePropertyToXML` | Direct sequential `TextWriter.Write` calls for scalar branches | 1,000-property integer XML about 21% faster; mixed 10,000-property XML about 9.5% faster with roughly 86% lower allocation in the matched fixture. |
| `WzBinaryWriter.WriteStringValue` | Single dictionary probe for cached strings | 4,096-character cached write: 16.747 → 15.659 µs; repeated-hit workload: 85.121 → 49.766 µs. |
| `WzBinaryWriter.Write(string)` | Span-based Unicode detection | ASCII 4,096 characters: 13.942 → 12.320 ns; Unicode 4,096 characters: 34.130 → 33.761 µs; allocations unchanged. |
| JSON/BSON serializers | Dictionary capacity sizing and `TryAdd` duplicate handling | JSON fixtures of 100/1,000/10,000 properties: 366.5 → 339.0, 628.8 → 552.0, and 3,004 → 2,888 µs; allocations unchanged. |
| Packet and session paths | Header buffer reuse, zero-length fast paths, reduced payload copies, and callback allocation reductions | Retained runs consistently reduced short-packet allocations and latency; wire-byte and lifecycle contracts remained identical. |
| WZ/MS file paths | Bulk UTF-16 reads/writes, span key handling, and direct ChaCha20/key transformations | Long List.wz payload reads improved about 47–49%; long payload writes improved up to about 22–25%; key-generation latency improved about 34–52%. |
| XML/NX/JSON utility paths | Direct scalar writes, indentation allocation removal, string-table probe reduction, and bitmap payload copy elimination | Retained runs reduced serializer allocations and latency; byte-for-byte golden outputs remained equal. |
| PNG/DXT decoders | Bounded DXT5/DXT3 inner loops and fused DXT3 alpha expansion | Small 8²/16² blocks showed roughly 45–80% lower latency and lower allocation; larger blocks stayed within noise. Full independent pixel oracles passed. |
| `WzLinkResolver` numeric canvas selection | Ordinal tracking instead of materializing a complete canvas list | 128/1024/4096 cases repeatedly improved about 4–11% and reduced allocations by 65–98%, while preserving traversal and failure behavior. |
| `ImageFormatDetector.AnalyzeImageData` | Bounded initial RGB hash-set capacity and row-stride arithmetic | Full 12-case run completed with bounded allocations; representative 64px opaque detection measured 28.09 µs / 170.51 KB. Output and format classification remained unchanged. |

The campaign reviewed the complete MapleLib function inventory. Functions without a repeatable gain, with behavior risk, or without an independent workload were left unchanged. The detailed ledger and disposable harnesses were removed after this aggregation.
## Additional retained ledgers

Earlier subsystem ledgers in `MapleLib/docs/perf/` contribute these retained results to the aggregate:

| Area / function | Measured gain |
|---|---|
| `LRUCache` hit path | About 51–52 ns → 22–23 ns (roughly 56% lower latency, 0 B allocation). |
| XML escaping (`XmlUtil.SanitizeText`) | Controlled live XML: Classic 2.974 → 2.194 ms (26.2%); Combined 3.082 → 1.944 ms (36.9%); allocations about 67% lower with identical hashes. |
| DXT3/DXT5 color and alpha indexing | Final 1024² confirmation: DXT3 14.589 → 8.060 ms (44.8%); DXT5 18.623 → 10.472 ms (43.8%); scalar fallback and intrinsic paths produced identical hashes. |
| `ByteUtils` compact hex parsing | 4 KiB parsing about 57.95 → 1.69–1.74 µs (33–34× faster) with about 98.5% lower allocation; portable fallback remained faster than baseline. |
| Enum/string helpers | Representative job-name formatting 184.07 → 71.42 ns; pre-BB names 83.27 → 23.78 ns; capitalization 12.92 → 5.258 ns; enum allocations reduced to zero. |
| IMG manager cache keys | Cached loads 47.77 → 33.09–34.27 ns (28–31% lower) and 136 → 56 B. |
| WZ wildcard search | 173.0 → 124.25–124.51 µs (about 28% lower), about 87% fewer allocated bytes; no-match allocation dropped over 99.9%. |
| WZ key bruteforce candidate filter | 239,282 ns / 17,664 B → 2.578 ns / 0 B per candidate in the measured workload; retained correctness checks cover multiple live WZ versions. |

These figures are summarized from the durable subsystem reports, whose source-level review and compatibility notes remain under `MapleLib/docs/perf/`.
