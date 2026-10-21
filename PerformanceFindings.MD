# Performance findings

Corpus-wide profile of the short Gold set, taken on 2026-10-21 at `aef2e544`. Each candidate is worked on one at a time; nothing here is changed yet.

## Baseline

- One rendering thread, ~30 s per run.
- 5.7 GB allocated over the run; GC pauses take 1.9–2.2 s (gen0 ~395, gen1 ~120, gen2 ~90 collections).
- Reducing allocations can recover 2 s at the very most; 0.5–1 s is the realistic target.
- About a third of the CPU time is native Skia drawing (`DrawPath`, text drawing, `Restore`, images). Image decoders take about 15%.
- `PdfDocumentObjectCache` lookups are ~0.05% of samples, and its dictionaries are not among the top allocations. The shape of its storage does not matter.

## Method

Gold is not changed. From `PdfPixel.Gold/bin/Release/net10.0`:

```bash
dotnet-trace collect --profile dotnet-common,dotnet-sampled-thread-time -o cpu.nettrace -- dotnet PdfPixel.Gold.dll ../../../ analyze
```

```bash
dotnet-trace collect --profile gc-verbose -o mem.nettrace -- dotnet PdfPixel.Gold.dll ../../../ analyze
```

`analyze` renders without loading, comparing or saving PNGs. The traces are read with a TraceEvent analyzer that charges every framework or Skia leaf frame to the nearest PdfPixel caller, so time spent in `Dictionary`, `MemoryStream` or a P/Invoke is billed to the method that called it.

Rows that are profiler bias, not costs:

- `Monitor.Enter_Slowpath` (~6%) is native time inside a `lock`: DeflateStream's own lock around zlib, and `ContentLocker` in `SkCanvasCommandProcessor.ExecuteCommand` around Skia. The run has one thread, so nothing waits on them.
- `PollGCWorker` (~8%) is the sampler suspending the thread.
- `CffCharStringEvaluator.TakeWidth`'s memmove (115 samples for one `RemoveAt(0)` per glyph) is very likely the same bias.

## Candidates

Sizes are the share of the run's allocations or samples.

### Allocations

- [x] **1. Content stream decoding** — `PdfStreamDecoder.DecodeContentStream`. The `MemoryStream` starts with no capacity and grows by doubling, then `ToArray()` copies the whole result again. ~280 MB, plus part of the 505 MB billed to `MemoryStream.set_Capacity`.
  - Done: one buffer sized from the raw length, no final copy; inline data is `byte[]? InlineData`, read without a copy and sliced from the parse buffer; a single content stream is no longer copied into a combined array. `/DL` is not read.
  - Short `analyze`, warm runs: baseline 27.2 / 27.9 s, change 28.6 / 26.9 / 27.6 s. No gain beyond run-to-run noise.
- [ ] **2. CFF and Type1 charstring operands** — `CffCharStringReader.ReadNextValue`, `Type1CharStringReader.ReadNextValue`. A `CffValue<T>` class is allocated for every operand and operator of every glyph. ~300 MB (`CffValue<int>` 211 MB, `CffValue<byte>` 51 MB, Type1 33 MB).
- [ ] **3. Shaped glyph copies** — `PdfTextRenderer`. `shapingResult.ToArray()` at three call sites, plus the `ShapedGlyph[]` and `PdfCharacter[]` arrays built per draw. ~226 MB of `ShapedGlyph[]`, ~61 MB of `PdfCharacter[]`.
- [ ] **4. Soft mask scope per path** — `PathRenderer.DrawPath`. A `SoftMaskDrawingScope` class is allocated for every path drawn, even when there is no soft mask. ~105 MB.
- [ ] **5. Character codes** — `PdfSingleByteFont.ExtractCharacterCodes`. `PdfCharacterCode` is a sealed class created once per character. ~95 MB.
- [ ] **6. CID to GID map** — `PdfCidToGidMap.ParseStreamData`. A `Dictionary<uint, ushort>` holds a dense map of at most 65,536 CIDs. ~51 MB.

### CPU

- [ ] **7. Path conversion** — `PdfPathConverter.ToSkPath`. One native call per path segment, plus a new `SKPathBuilder` per path. ~4% inclusive.
- [ ] **8. AES** — `AesCbc` (`MixColumns`, `EncryptBlock`, `AddRoundKey`, `SubBytes`). Hand-written AES instead of the hardware-backed `System.Security.Cryptography.Aes`, which is not available in the browser. Needs a decision first. ~1.5%.
- [ ] **9. Averaging downsample** — `AveragingDownsampleRowConverter.TryConvertRow`. The largest single PdfPixel function at 5.3% of samples in its own code; may be worth vectorizing.
- [ ] **10. Glyph list lookups** — `AdobeGlyphList.TryGetUnicode`, with `PdfFontString` hashed through XxHash32. ~0.4%.

## Gold only

Not library costs; they only affect the Gold run itself.

- `File.ReadAllBytes` of every PDF: 204 MB.
- `PurgeSkiaCaches` clears Skia's font cache between PDFs, so every document rebuilds its glyph caches.
