# tunetag — Project Plan

## Scope

A small, cross-platform (Windows 10/11 + macOS) desktop utility for editing audio file
metadata in bulk, plus a headless CLI sharing the same engine. Core value is offline and
local: read/write tags, manage album art, and rename files from tags. An optional
local-AI assist can suggest missing metadata but is never required.

**In scope**
- Formats: MP3 (ID3v2.3/2.4), FLAC & Ogg Vorbis (Vorbis Comments), M4A/AAC/ALAC (MP4
  atoms), WAV (RIFF INFO / ID3).
- Batch editing of common fields (title, artist, album, album-artist, track/disc #,
  year, genre, composer, comment).
- Album art: embed, extract, replace, remove; apply one cover to a whole folder.
- Rename files & build folder structure from a tag template; reverse-parse filenames.
- Consistency audit (missing fields / art / track numbers).
- Optional local-AI metadata suggestions via Ollama/llama.cpp (opt-in, local-only).

**Out of scope (Non-goals)** — see below.

## Architecture / tech approach

- **Language/runtime:** .NET 8.
- **UI:** Avalonia UI (MVVM) for a genuinely cross-platform desktop shell (Windows +
  macOS from one codebase). WPF rejected as Windows-only.
- **Core library — `TuneTag.Core` (UI-free, unit-tested):**
  - `ITagReader` / `ITagWriter` → normalized `TrackTags` model (`Field{Name,Value}`,
    `AlbumArt{Mime,Bytes,Kind}`), format-agnostic to callers.
  - `IAudioFileFormat` backends behind a `FormatRouter` (extension/magic-byte sniffing).
  - Tagging backend: **TagLibSharp** as the primary implementation behind the interfaces,
    keeping the abstraction so a backend swap is possible.
  - `IArtService` — embed/extract/replace/remove cover art; folder-wide apply.
  - `IRenameEngine` — template tokens (`{artist}`,`{album}`,`{title}`,`{track:00}`,
    `{year}`,`{genre}`) → new path; two-phase apply with collision handling + JSON undo
    journal.
  - `IFilenameParser` — reverse-parse filenames into tag suggestions.
  - `IConsistencyAuditor` — report missing/inconsistent fields across a selection.
  - `ITagAiService` — optional; calls a local OpenAI-compatible endpoint, returns
    *proposals* only. Reachability probe + graceful fallback; disabled by default.
- **CLI — `TuneTag.Cli`:** thin wrapper over Core (`inspect`, `set`, `art`, `rename`,
  `audit`), dry-run by default for destructive ops.
- **App — `TuneTag.App`:** Avalonia grid (virtualized DataGrid) for multi-file edit, an
  album-art panel, rename dialog with live preview, and an AI-suggestions review pane.
- **Persistence:** JSON settings + undo journals under `%APPDATA%\tunetag` (Windows) and
  `~/Library/Application Support/tunetag` (macOS).
- **Safety:** all destructive operations (write, rename, art removal) are two-phase with
  an undo journal; batch writes are transactional per-file with error collection.
- **Testing:** xUnit against `TuneTag.Core` with small synthetic audio fixtures per format.

## Milestones

1. **M1 — Core engine & model:** interfaces, `TrackTags`, TagLibSharp-backed read/write
   for MP3/FLAC/M4A, `FormatRouter`, round-trip tests.
2. **M2 — CLI:** `inspect`/`set`/`art`/`rename`/`audit`, dry-run defaults, exit codes.
3. **M3 — Grid UI:** open folder, virtualized batch-edit grid, multi-select edit, save.
4. **M4 — Album art:** embed/extract/replace/remove + folder-wide apply, UI panel.
5. **M5 — Rename engine:** template tokens, live preview, two-phase apply + undo;
   filename→tags reverse parsing.
6. **M6 — Local-AI assist:** `ITagAiService`, Ollama/llama.cpp client, review-and-approve
   UX, reachability probe + fallback, off by default.
7. **M7 — Packaging & CI:** Windows portable zip + MSIX, macOS `.app`/`.dmg`, GitHub
   Actions matrix (windows-latest + macos-latest) building + testing both.

## Non-goals

- Not a music player, library organizer/DJ, or streaming client.
- Not an online metadata lookup service (no MusicBrainz/Discogs/AcoustID scraping in v1;
  could be a future opt-in plugin).
- No cloud sync, accounts, or telemetry.
- Not a format transcoder/converter (that's a separate tool).
- Not a duplicate finder or disk cleaner (covered by other tool-lab utilities).

## Packaging / distribution

- **Windows:** self-contained `win-x64` portable zip + MSIX package.
- **macOS:** `.app` bundle + `.dmg` (x64 and arm64; universal if feasible).
- **CI:** GitHub Actions matrix builds artifacts for both OSes and runs `dotnet test`.
- Releases attach per-OS artifacts; versioned via tags.
