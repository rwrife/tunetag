# tunetag

Cross-platform desktop **music tag editor** for Windows 10/11 and macOS. Batch-edit
audio metadata (ID3v2 / Vorbis Comments / MP4 atoms), embed and manage album art,
and rename/organize files from their tags — all locally, with an optional local-AI
assist for filling in missing metadata. **Offline & privacy-first: no cloud, no account.**

> Status: 🚧 Early scaffold. Docs and backlog first; implementation lands issue-by-issue.

## Overview

tunetag gives you a fast, spreadsheet-style grid for editing the tags on your music
library. Drop in a folder, select tracks, and edit title/artist/album/track#/year/genre
and more across many files at once. Fix inconsistent album names, add cover art in bulk,
strip junk comments, and rename files into a clean `Artist/Album/## - Title` layout —
without uploading anything anywhere.

Supported formats (target): **MP3 (ID3v2.3/2.4), FLAC & Ogg (Vorbis Comments),
M4A/AAC/ALAC (MP4 atoms), and WAV (INFO/ID3)**.

## Motivation

Music libraries accumulate messy metadata: "Various Artists" chaos, missing album art,
`Track 01` filenames, mojibake, and duplicate/junk comment frames. Existing taggers are
often Windows-only, cloud-tied, bloated, or abandoned. tunetag is a small, modern,
cross-platform utility that does the common jobs well, keeps everything on your machine,
and treats an optional local LLM as a convenience — never a requirement.

## Use cases

- **Batch cleanup** — normalize album/artist/genre across an album or whole library.
- **Album art** — embed a cover into every track in a folder; extract/replace/remove art.
- **Rename from tags** — generate filenames and folder structure from a template.
- **Tags from filename** — reverse-parse `01 - Artist - Title.mp3` into proper tags.
- **De-junk** — strip comment/encoder/rating frames and private tags in bulk.
- **Consistency audit** — flag tracks with missing title/artist/art/track numbers.
- **Optional AI fill** — suggest missing genre/album/title from existing metadata using a
  local model, with you approving every change.

## How to use

### Windows 10/11 quickstart
1. Download the latest `tunetag-win-x64-portable.zip` from Releases (or build from source).
2. Unzip and run `tunetag.exe` (portable, no install), or install `tunetag-win-x64.msix`.
3. **File → Open Folder…** to load your music, edit in the grid, then **Save**.

### macOS quickstart
1. Download either `tunetag-macos-x64.dmg` (Intel) or `tunetag-macos-arm64.dmg` (Apple Silicon) from Releases.
2. Drag **tunetag.app** to Applications and launch (unsigned builds: right-click → Open).
3. **File → Open Folder…**, edit, **Save**.

### Build from source (both platforms)
```bash
# Requires .NET 8 SDK
git clone https://github.com/rwrife/tunetag.git
cd tunetag
dotnet build
dotnet run --project src/TuneTag.App        # desktop app (Avalonia)
dotnet run --project src/TuneTag.Cli -- --help   # headless CLI
dotnet test                                  # unit tests on TuneTag.Core
```

## Example workflow

```bash
# Headless CLI (scriptable; same engine as the GUI)

# Inspect tags for a folder
tunetag inspect ~/Music/Album

# Set album + year across every track in a folder
tunetag set --album "Kind of Blue" --year 1959 ~/Music/Album/*.flac

# Embed a cover into all tracks
tunetag art --set cover.jpg ~/Music/Album

# Rename files from tags using a template (dry-run by default)
tunetag rename --template "{track:00} - {title}" ~/Music/Album
tunetag rename --template "{track:00} - {title}" --apply ~/Music/Album
```

In the GUI the same operations are available as grid edits, an album-art panel, and a
rename dialog with a live old→new preview and a safe two-phase apply + undo.

## Local-AI integration (optional)

tunetag works fully in **non-AI mode**. When enabled, it talks to a local
OpenAI-compatible endpoint (**Ollama** or **llama.cpp**) to suggest missing metadata —
e.g. inferring genre from artist/album, cleaning up a mojibake title, or proposing an
album name. Only text metadata you choose is sent, always to `localhost`, and **every
suggestion is a proposal you approve** before it's written.

- Tiny/small models work well: Llama 3.2, Qwen2.5, Phi-3-mini, MiniCPM class.
- A reachability probe disables AI features gracefully when no local model is running.
- Off by default; no network calls in the default configuration.
- Payload contract: see [`docs/local-ai-payload.md`](docs/local-ai-payload.md).

## Current status / milestones

- [ ] M1 — Core tag read/write engine + domain model (MP3/FLAC/M4A)
- [ ] M2 — Headless CLI (inspect/set/art/rename)
- [ ] M3 — Avalonia grid UI with multi-file batch editing
- [ ] M4 — Album art management (embed/extract/replace/remove)
- [ ] M5 — Rename-from-tags + tags-from-filename with preview & undo
- [ ] M6 — Optional local-AI metadata assist (off by default)
- [ ] M7 — Packaging & CI (Windows zip/MSIX, macOS .app/.dmg)

## License

MIT
