# ParallelCompare User Guide

This guide explains how to run comparisons with the ParallelCompare CLI and interactive TUI, configure behavior, and interpret results.

## 1. Overview
ParallelCompare inspects two directory trees in parallel, highlighting mismatches in structure, metadata, or file contents. The command-line interface powers automation while the Spectre.Console TUI provides a rich interactive review experience.

Key capabilities include:
- Multi-threaded comparisons with configurable hashing strategies.
- Baseline snapshots that allow left-only comparisons with stored results.
- Live watch mode that re-runs comparisons as files change.
- Exporters for JSON, Markdown, JUnit, and custom formats.
- Diff tool integrations for one-click file inspection from the TUI or CLI output.

## 2. CLI usage
Run `parallelcompare compare` with two paths or a path plus baseline snapshot:

```bash
parallelcompare compare /path/to/left /path/to/right --threads 8 --hash sha256
```

Common options:
- `--threads <n>`: Override default thread count.
- `--hash <algo>`: Select `sha1`, `sha256`, or `xxhash` content hashing.
- `--ignore <pattern>`: Apply glob patterns (repeat for multiple entries).
- `--baseline <file>`: Compare a live tree against a saved baseline snapshot.
- `--diff-tool <command>`: Launch a diff tool for mismatched files.
- `--no-progress`: Disable the live progress renderer for CI logs.

### Watch mode
```
parallelcompare compare src build --watch --watch-delay 2
```
- Automatically re-runs the comparison when either side changes.
- The first run produces a full report; subsequent runs show incremental summaries.
- Combine with exporters to capture artifacts on every cycle.

### Snapshot creation & reuse
```
parallelcompare snapshot src --output baselines/linux.json
parallelcompare compare src --baseline baselines/linux.json
```
- Snapshots capture structure, metadata, and hash signatures.
- Baseline comparisons treat missing files as regressions and highlight drift.

### Exporters
```
parallelcompare compare left right \
  --json reports/diff.json \
  --junit reports/tests.xml \
  --markdown reports/summary.md
```
- Exporters can be combined; each flag writes to its target path.
- Use `--summary` for condensed Markdown suitable for release notes.

## 3. Interactive TUI
Launch the Spectre-based interface with `--interactive` or via the dedicated command:

```bash
parallelcompare compare left right --interactive
```

TUI essentials:
- **Navigation**: arrow keys / `j` `k` traverse nodes; `enter` drills into children.
- **Filters**: press `f` to toggle difference filters (`All`, `Differences`, `Left Only`, `Right Only`).
- **Algorithms**: `a` cycles hash strategies live; results refresh automatically.
- **Exports**: `e` opens the exporter panel to trigger configured outputs.
- **Snapshots**: `s` captures the current comparison to a snapshot file.
- **Diff tool**: `d` launches the configured external diff tool for the selected node.
- **Pause/Resume**: space bar pauses watch mode or live updates.
- **Help**: `?` displays all key bindings and tips.

The status bar surfaces watch mode activity, baseline metadata, and exporter success/failure notifications.

## 4. Configuration hierarchy
ParallelCompare loads settings in the following order (later entries override earlier ones):
1. Default application settings (theme, hash algorithm, ignore patterns).
2. Machine-level config (`/etc/parallelcompare/config.json` or `%ProgramData%\ParallelCompare\config.json`).
3. User-level config (`~/.config/parallelcompare/config.json` or `%AppData%`).
4. Repository profile (`.parallelcompare/profile.json`).
5. Command-line options.

Profiles can include:
- Default exporters and output paths.
- Theme preferences (`dark`, `light`, `high-contrast`).
- Custom ignore sets for common build artifacts.

## 5. Diff tool integration
Configure a preferred diff command via CLI or config file:

```json
{
  "diffTool": "code --diff {left} {right}"
}
```

Placeholders supported: `{left}`, `{right}`, `{path}`, `{line}`, `{column}`.

The CLI prints the command it executes when `--verbose` is enabled. In the TUI, `d` triggers the diff tool for the focused node and shows status feedback in the notification tray.

## 6. Troubleshooting & tips
- Use `--verbosity trace` to capture detailed logs when diagnosing configuration issues.
- For large directories, increase `--threads` and consider `--hash xxhash` for faster scans.
- Combine watch mode with baselines to monitor regressions during long-running builds.
- If the terminal does not support truecolor, set `--theme high-contrast` for better readability.
- Export snapshots regularly to compare against known-good releases.

## 7. Additional resources
- `parallelcompare --help` for global options.
- `parallelcompare compare --help` for command-specific flags.
- `docs/manual-validation.md` for the latest cross-platform validation notes.
- `docs/release-notes.md` for highlights and migration guidance per release.
