# Audio Studio architecture

Audio Studio is a non-destructive MapleStory audio workflow shared by HaCreator and HaRepacker. Source WZ/IMG images and external files are represented by `AudioSourceReference`; decoded sample buffers are transient and are never serialized into a `.hasound.json` project.

## Project and recovery

Projects use `schemaVersion` and a stable `projectId`. Tracks, clips, buses, markers, regions, stem groups, master format, and source metadata are serialized with `System.Text.Json`. External paths are relative to the project by default; Collect Media copies them into `media/` and rewrites references. Native WZ/IMG references retain source-set identity, image/property paths, physical casing, and a content hash. Reopen validates hashes and reports relink diagnostics instead of mutating sources. Autosave writes a sibling recovery file and can be promoted atomically.

Undo/redo is command based. Commands capture only project state changes and can be replayed without loading decoded audio. The same command stream drives editor gestures, keyboard shortcuts, and automation changes.

## Engine and caches

`HaSharedLibrary.Audio` normalizes MP3 and PCM WAV (including native WZ payloads) to non-interleaved float samples while preserving original format and declared/decoded duration metadata. Codec providers return diagnostics for malformed, truncated, unsupported, or duration-mismatched payloads. Preview transport is cancellable and seekable; offline rendering uses the same render graph and remains separate from the existing MonoGame runtime.

Waveform data is stored as bounded multi-resolution min/max/RMS peak pyramids. Cache keys include source hash, decode format, and resolution; spectrogram tiles use an independent cache. Both caches support cancellation, disk eviction, and corruption recovery.

## WZ/IMG bake semantics

Bake renders to a temporary stream, creates a new `WzBinaryProperty`, and replaces or adds only the requested property. Existing siblings and unknown properties remain untouched. Image casing is resolved through `.imgcase.json`; serialization uses the existing atomic save/backup path. On failure the in-memory tree and dirty state are restored. A successful bake reopens and validates the resulting property before returning.

## Integrations and comparison

The HaCreator catalog recursively indexes Sound images and UOL links using metadata-first enumeration. Map audio (`bgm`, `AmbientBGM`, `AmbientBGMv`, `bgmSub`) and cutscene references project through the same catalog and preserve original spelling. HaRepacker exposes sound-property actions for opening, exporting, replacing, and baking.

An active source set and read-only comparison source set can be indexed concurrently. Comparison is case-insensitive while preserving destination casing and reports added/removed/changed assets, metadata/hash differences, and copy conflicts.

## AI-assisted generation

Audio Studio AI is a local-first extension of the non-destructive workflow. Provider output is always a candidate file; it does not bypass project undo, export validation, playback, or explicit WZ/IMG baking.

### Shared contracts and providers

`HaSharedLibrary.Audio.AI` contains provider-neutral briefs, capability/model discovery, jobs, events, artifacts, upload authorization, provenance, provider selection, job persistence, prompt compilation, and secret-store contracts. Canonical objects are versioned and tolerate provider-specific extension data without allowing it to replace canonical fields.

`AceStepLocalAudioAiProvider` connects to a loopback ACE-Step-compatible REST sidecar. `AudioAiSidecar` can supervise a user-selected executable with a random launch token, startup health checks, redirected output, cancellation, and process-tree disposal. It never terminates a user-managed endpoint.

Closing Audio Studio disposes only the managed sidecar it launched. Disposal first requests an optional graceful HTTP shutdown and closes any provider window, then waits briefly for model workers to unload. A process-tree kill is retained as a bounded fallback so GPU memory and orphaned workers cannot survive the editor window. Downloaded model weights remain cached on disk.

The default network policy is `LocalOnly`. Cloud reference artifacts require an `UploadAuthorization` scoped to provider, artifact IDs, byte count, purpose, and expiry. The OpenAI-compatible planner accepts text only and cannot render or upload reference audio.

### Job lifecycle and provenance

Each job can persist `request.json` and `state.json` beneath an independent job directory. Secret values are never part of these models. Candidates are validated and hashed before acceptance. Durable accepted media belongs in the normal project media folder; temporary job artifacts are not authoritative project sources.

### UI, prompt formatting, and output

The Audio Studio AI tab exposes a localized local ACE-Step generation surface. It checks health, submits a canonical instrumental loop brief, streams progress, and imports only returned local files. Generated media remains subject to normal Audio Studio project, playback, export, and bake behavior.

The generation surface accepts a 10–600 second requested duration and WAV or MP3 output. Both values are represented in the canonical brief. For the managed ACE-Step installation, MP3 requests are generated as WAV and encoded locally with the shared Windows Media Foundation codec because the managed runtime does not assume `ffmpeg` is installed. Returned and imported files are metadata-probed before timeline insertion so the clip uses the decoded media duration instead of the editor's four-second unknown-duration fallback.

Users can optionally refine a raw music idea with the configured OpenAI-compatible text model before generation. The refinement prompt is music-specific, preserves the user's intent, and returns only a production-ready generation brief; the original text remains available through normal undo/edit behavior.

Text-model refinement is recommended but never required. If no compatible text model is configured, Audio Studio offers to open the shared AI settings and lets the user decline and generate from the raw brief. Animation Studio follows the same optional formatting policy for its prompt-suggestion action.

The managed HaCreator flow can install the runtime after explicit user confirmation. Installation presents the model download size before proceeding, and model weights download lazily on first generation; discovery and health checks alone never download weights. Managed files live under `%LocalAppData%\HaCreator\AudioAI\ACE-Step-1.5`, and Audio Studio exposes the path plus open-folder and delete controls.

## Validation and performance

Malformed assets produce item-level warnings. Selecting a new asset cancels prior decode and waveform work. Indexing never decodes every payload, and cache bounds keep large clients (including the multi-gigabyte modern Sound tree) browsable. Automated tests cover codecs, editing, transport, project schema/recovery, recursive links, map/cutscene integration, bake rollback, and source comparison.

Audio Editor tests additionally cover canonical AI prompt defaults, provider selection, upload scoping, persistence, schema rejection, empty-artifact failure reporting, MP3 codec fallback, and a live loopback HTTP sidecar round trip. A real ACE-Step generation test requires a compatible Python/model installation.
