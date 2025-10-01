# FsEqual User Guide

FsEqual (`fsequal`) compares directory trees quickly while offering a full interactive inspector. This guide summarizes how to install, configure, and operate the consolidated experience across CLI, watch mode, snapshots, and exporters.

## Installation

```bash
# .NET global tool
dotnet tool install --global FsEqual.Tool

# Upgrade to the latest release
dotnet tool update --global FsEqual.Tool
```

For portable scenarios, use `dotnet publish src/FsEqual.Cli -c Release -r <rid>` and distribute the platform-specific binaries.

## Configuration Hierarchy

Settings merge in the following order:

1. Global config: `%APPDATA%/fsequal/config.json` or `$XDG_CONFIG_HOME/fsequal/config.json`.
2. User config: `~/.config/fsequal/config.json`.
3. Project config: `.fsequal/config.json` at the repository root.
4. Command-line options.

Profiles can predefine common baselines, ignore lists, exporter bundles, and hash preferences:

```json
{
  "profiles": {
    "ci": {
      "threads": 8,
      "hash": "xxh128",
      "exporters": [
        { "type": "json", "path": "artifacts/report.json" },
        { "type": "markdown", "path": "artifacts/summary.md" }
      ]
    }
  }
}
```

Activate with `fsequal compare --profile ci A B`.

## Core Commands

### Compare

Compare two directories, honoring ignores, threads, and exporters:

```bash
fsequal compare A B --threads 8 --ignore "**/bin/**" --export json=out/report.json
```

Key options:

- `--hash <algo>` (`none`, `xxh128`, `sha256`) controls content hashing.
- `--baseline <path>` compares against a recorded snapshot.
- `--diff <tool>` launches an external diff command when selecting files in the TUI.
- `--profile <name>` applies saved settings.

### Watch

Stay in sync with changes, re-running comparisons when files mutate:

```bash
fsequal watch A B --debounce 500ms --interactive
```

Highlights:

- Batched updates with pause/resume (`space`) and manual refresh (`r`).
- Displays baseline metadata, filter state, and exporter status in the header.
- Supports headless mode with structured summaries for CI (`--no-interactive`).

### Snapshot

Create baselines or compare against them:

```bash
# Create snapshot
fsequal snapshot create ./baseline.json --source ./golden

# Verify against snapshot
fsequal snapshot compare ./baseline.json --target ./candidate --interactive
```

Snapshots store metadata, file hashes, and ignore policies so subsequent compares are deterministic.

### Completion

Generate shell completions:

```bash
fsequal completion zsh > "${fpath[1]}/_fsequal"
```

Bash, Zsh, Fish, and PowerShell are supported.

## Interactive Explorer (TUI)

Launch the Spectre.Console interface with `--interactive` (default for watch mode). Key bindings:

| Action | Key |
| --- | --- |
| Navigate tree | Arrow keys / `h` `j` `k` `l` |
| Expand/collapse | `enter` or `space` |
| Toggle filters | `f` (status), `m` (mismatches), `s` (same), `u` (unknown) |
| Switch algorithm | `a` |
| Launch diff tool | `d` |
| Export reports | `e` |
| Pause/resume watch | `p` / `space` |
| Search | `/` |
| Help overlay | `?` |

The status bar reflects active profile, hash algorithm, baseline info, and exporter results. Visual themes (`--theme <name>`) support light, dark, and high-contrast palettes.

## Exporters

Configure exporters per command or via profiles:

```bash
fsequal compare A B \
  --export json=out/report.json \
  --export markdown=out/summary.md \
  --export csv=out/diff.csv
```

Exporters run incrementally during interactive sessions to keep reports up to date. When using watch mode, exports update after each completed cycle.

## Quickstart Scenarios

1. **Baseline verification**
   ```bash
   fsequal compare build/stable build/candidate --baseline baselines/release.json --profile ci
   ```
2. **Live watch during refactor**
   ```bash
   fsequal watch src refactor --interactive --threads 4 --ignore "**/obj/**"
   ```
3. **Snapshot creation for release artifacts**
   ```bash
   fsequal snapshot create baselines/1.0.0.json --source dist
   ```
4. **Headless CI report**
   ```bash
   fsequal compare . ../main --no-interactive --export json=artifacts/report.json --export markdown=artifacts/summary.md
   ```
5. **Investigate differences with diff tool**
   ```bash
   fsequal compare A B --diff "code --diff {left} {right}" --interactive
   ```

## Troubleshooting

- Set `--verbosity trace` to capture detailed diagnostics.
- Use `--no-progress` in CI to avoid control characters in logs; otherwise the live panel streams MB/s, files/s, total bytes, and queued files in real time.
- Switch the final tree with `--summary-filter all|differences|left|right|errors` when you need more or less detail.
- If diff tools are not discovered automatically, specify the absolute path via `--diff`.
- When running over network shares, consider `--hash sha256` for stronger verification.

## Additional Resources

- [Manual Cross-Platform Validation](manual-validation.md)
- [Release Notes](release-notes.md)

