# fsEqual — Full Plan (CLI + TUI with Spectre.Console)

## 0) High-level

* **Purpose**: Fast, multi-threaded, channel-based folder comparison.
* **Faces**:

  * **CLI** for automation/CI and scripted runs.
  * **TUI** (terminal UI) for interactive exploration: progress bars, live tables, drill-down, filters.
* **Hash algos**: `crc32` (default), `md5`, `sha256`, `xxh64` (optional plugin).
* **Logging**: Info/Warn/Error (+ trace/debug), verbosity configurable. Structured output, optional JSON.

---

## 1) Installation & Invocation

* **Dotnet tool**:
  `dotnet tool install -g FsEqual.Tool` → invokes `fsequal`
* **Single-file binaries** (Win/macOS/Linux): unzip → `./fsequal`
* **Help**: `fsequal --help` (Spectre rich help), `fsequal tui --help`, `fsequal compare --help`

---

## 2) Modes

### A) CLI Mode (non-interactive)

Primary command: `compare`

```
fsequal compare <left> <right>
  [-t|--threads <int>]
  [-a|--algo <crc32|md5|sha256|xxh64>]
  [-m|--mode <quick|hash>]
  [-i|--ignore <glob>...]             # repeatable
  [--case-sensitive] [--follow-symlinks]
  [--mtime-tolerance <seconds>]       # only in quick mode
  [-v|--verbosity <trace|debug|info|warn|error>]
  [--json <path>]                     # detailed machine-readable report
  [--summary <path>]                  # small summary JSON
  [--no-progress]                     # disable live progress bars
  [--timeout <seconds>]
  [--fail-on <any|diff|error>]        # maps to exit codes
  [--profile <name>] [--config <file>]
```

**Exit codes**

* `0` Equal
* `1` Differences found (no fatal errors)
* `2` Runtime error / aborted (IO error, cancellation, etc.)

**Typical runs**

* Fast sanity check, default threads & algo:
  `fsequal compare ./A ./B`
* Fully hashed with 8 workers, ignore build junk:
  `fsequal compare ./A ./B -t 8 -a crc32 -m hash -i "**/bin/**" -i "**/obj/**"`
* Quick mode with mtime slop (2s):
  `fsequal compare ./A ./B -m quick --mtime-tolerance 2`
* Machine output for CI:
  `fsequal compare ./A ./B --json out/report.json --summary out/summary.json --no-progress -v warn`

**CLI Output UX (Spectre.Console)**

* Live **progress**: tasks for Enumerate, Queue, Hashing, Aggregation.
* Final **panel** with totals: files compared / equal / diffs / missing / errors / duration.
* **Table** of differences (paginated or truncated with “show more” hint), columns:

  * Type (MissingLeft/MissingRight/TypeMismatch/SizeMismatch/HashMismatch)
  * Path (relative)
  * Size(L/R)
  * Algo (if hashed)
* Respect `--verbosity` for how much detail is printed.

---

### B) TUI Mode (interactive)

Command: `tui`

```
fsequal tui <left> <right>
  [-t|--threads <int>]
  [-a|--algo <crc32|md5|sha256|xxh64>]
  [-m|--mode <quick|hash>]
  [-i|--ignore <glob>...]
  [--case-sensitive] [--follow-symlinks]
  [-v|--verbosity <...>]
  [--profile <name>] [--config <file>]
```

**TUI Layout (Spectre.Console)**

* **Header**: left/right paths, algo, mode, workers, elapsed.
* **Left pane (Tree)**: directory tree with status glyphs:

  * ✓ equal, ! diff, – missing, ? error
* **Right pane (Table)**: details for selected node:

  * For dirs: child summary (counts by status) + last few diffs
  * For files: type/size/mtime/hash (if available), status reason
* **Footer / keybinds**:

  * `↑/↓/←/→` navigate
  * `F` filter (status/type/path)
  * `A` toggle algo on-the-fly (re-queue selected subset)
  * `R` re-compare selection
  * `E` export current view (`json/csv/markdown`)
  * `L` cycle verbosity
  * `P` pause/resume pipeline
  * `Q` quit, `?` help
* **Live progress** bar cluster at top or bottom; errors appear in a collapsible log panel.

**TUI Workflows**

* “Just show me diffs”: start → press `F` → pick `Diff!=None` → browse.
* “Deep dive a directory”: select folder → `Enter` to expand → choose file → view both sides’ metadata.
* “Re-hash only this dir with SHA-256”: select dir → `A` → choose `sha256` → re-queue subset.

---

## 3) Comparison Semantics

* **Exists?** Missing on either side → difference.
* **Type mismatch** (file vs dir) → difference.
* **Quick**: equal size & mtime within tolerance → equal (no hash).
* **Hash**: equal size → compute hashes (selected algo) and compare.
* **Symlinks**: skipped unless `--follow-symlinks`; compare targets if followed.
* **Permissions/ACLs**: out of scope v1 (note for future).
* **Case sensitivity**: default OS behavior; override with `--case-sensitive`.

---

## 4) Performance & Scaling

* **Threads**: default `Environment.ProcessorCount`. Override `-t`.
* **Channels**: bounded; pipeline stages: enumerate → queue → N hash workers → aggregate.
* **Short-circuit**: size mismatch avoids hashing.
* **Large files**: read in 128–256 KiB blocks; async IO.
* **Cancellation**: `--timeout` or Ctrl+C → graceful drain.

---

## 5) Logging & Verbosity

* Levels: `trace|debug|info|warn|error`.
* **CLI**:

  * `info`: progress + final table + key differences
  * `warn`: suppress routine info; show anomalies (skipped/permission)
  * `error`: only errors + summary
  * `debug/trace`: per-file decisions (heavy; for diagnosing)
* **TUI**: Log panel with filter; same levels.

---

## 6) Output & Artifacts

* **Console**: Spectre tables/panels/markup.
* **Files**:

  * `--json`: full per-file results (status, reason, sizes, hashes if computed).
  * `--summary`: totals + config (good for dashboards).
  * TUI `E` export: `json|csv|md` for the current filtered view.
* **Determinism**: sorted by relative path in exports.

---

## 7) Ignore & Include

* `-i/--ignore` supports globs; repeatable.
* Optional `--include <glob>` (future).
* Reads `.fsequalignore` in either root unless `--no-ignore-file`.

---

## 8) Config & Profiles

* Config file (YAML or JSON), default search:

  * Local: `<left>/.fsequal.yml`, `<right>/.fsequal.yml`
  * Global: `~/.config/fsequal/config.yml`
* **Profiles**:

  ```yaml
  profiles:
    ci:
      algo: crc32
      threads: 8
      mode: hash
      ignore: ["**/bin/**","**/obj/**",".git/**"]
      verbosity: warn
  ```
* Use: `--profile ci` (CLI or TUI).

---

## 9) Algorithms (pluggable)

* Built-ins: `crc32`, `md5`, `sha256`.
* Optional: `xxh64` via plugin package `FsEqual.Hash.Xxh64`.
* CLI discovery: `fsequal algos` → list available.
* Per run: `-a sha256`. Per TUI: `A` cycles/chooses for selection.

---

## 10) CI/CD Recipes

**GitHub Actions (summary + artifact)**

```yaml
- name: Compare dist folders
  run: fsequal compare ./dist-prev ./dist-new -t 8 -a crc32 -m hash \
       -i "**/*.map" --json out/report.json --summary out/summary.json \
       --fail-on diff --no-progress -v warn

- name: Job Summary
  run: |
    echo "### fsEqual Summary" >> $GITHUB_STEP_SUMMARY
    jq -r '"Equal: \(.equal), Diff: \(.differences), Errors: \(.errors)"' out/summary.json >> $GITHUB_STEP_SUMMARY

- uses: actions/upload-artifact@v4
  with:
    name: fsequal-report
    path: out/
```

**Pre-release verification**

```
fsequal compare ./release-A ./release-B -i "**/docs/**" -v info --fail-on any
```

---

## 11) UX Details (Spectre bits)

**CLI visuals**

* Progress: `Progress().AutoClear(false).Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new RemainingTimeColumn())`
* Tables: `Table` for differences (truncate to N rows, print hint to `--json` for full list).
* Panels: Summary panel with stats & emoji status (why not a little sparkle).

**TUI visuals**

* Tree view: glyphs for status; color-coded nodes.
* Hotkeys overlay (`?`) uses Spectre’s key capture.
* Live refresh on pipeline events; batched UI updates to avoid flicker.

---

## 12) Security & Safety

* Open files with `FileShare.Read`.
* Respect `.gitignore` optionally (future flag).
* Warn on unreadable paths; continue (unless `--fail-on any`).

---

## 13) Roadmap

* **V1**: CLI + TUI, CRC32/MD5/SHA-256, ignores, profiles, JSON export, multi-thread.
* **V1.1**: XXH64 plugin, include globs, `.gitignore` opt-in, blockwise byte-compare pre-hash.
* **V1.2**: Remote compare (hash stream over gRPC/SSH), permissions/ACL compare opt-in.
* **V2**: Snapshot manifests, change reports, watch mode.

---

## 14) Minimal Skeleton (just enough to situate things)

```
fsEqual/
  src/
    FsEqual.Cli/                 # Spectre.Console.Cli commands
      Commands/
        CompareCommand.cs        # CLI pipeline runner + outputs
        TuiCommand.cs            # TUI host (interactive)
        AlgosCommand.cs
      Options/
        CompareSettings.cs
      Ui/
        CliRenderers.cs
        TuiScreens.cs
    FsEqual.Core/                # Pipeline + domain
      Pipeline/ (channels)
      Hashing/ (IContentHasher + built-ins)
      Model/ (results, enums)
      Config/ (profiles, loader)
      Logging/ (wrappers)
  tests/
    FsEqual.Tests/
      CliUsageTests.cs
      PipelineTests.cs
      HashTests.cs
```

---

## 15) Quickstart Cheatsheet

* Compare two folders with 8 threads:

  ```
  fsequal compare A B -t 8
  ```
* Ignore build outputs and only warn:

  ```
  fsequal compare A B -i "**/bin/**" -i "**/obj/**" -v warn
  ```
* Quick mode with mtime tolerance:

  ```
  fsequal compare A B -m quick --mtime-tolerance 2
  ```
* Interactive TUI:

  ```
  fsequal tui A B
  ```
* Export machine-readable report:

  ```
  fsequal compare A B --json out/report.json --summary out/summary.json --no-progress
  ```

