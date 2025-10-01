# `fsequal compare`

The `compare` command is the primary entry point for analyzing two directory trees (or a tree against a saved baseline). It resolves configuration profiles, merges ignores, and can launch an interactive session or produce structured exports for automation.

## Usage

```bash
fsequal compare <left> [right]
```

- `<left>` – Required path to the primary directory.
- `[right]` – Optional path to compare against. When omitted, `--baseline` must point to a saved manifest.

## Key options

| Option | Description |
| --- | --- |
| `-a|--algo <name>` | Adds a hash algorithm (`crc32`, `sha256`, `md5`, `xxh64`). Repeat to add multiple algorithms. |
| `-m|--mode <mode>` | Sets the comparison mode (`quick` or `hash`). Defaults to `quick`. |
| `-i|--ignore <pattern>` | Adds glob patterns (e.g. `**/bin/**`) to exclude. Repeat to specify multiple patterns. |
| `--case-sensitive` | Treats file and directory names as case sensitive. |
| `--follow-symlinks` | Traverses symbolic links instead of skipping them. |
| `--mtime-tolerance <seconds>` | Accepts modified-time differences within the provided tolerance. |
| `--baseline <path>` | Compares the left tree against a previously captured baseline manifest. |
| `--json <path>` / `--summary <path>` | Writes detailed or summary JSON reports. |
| `--export <name>` | Runs an exporter bundle defined in configuration. |
| `--diff-tool <path>` | Launches an external diff tool when selecting files in the TUI. |
| `--profile <name>` | Applies a named configuration profile. |
| `--interactive` | Opens the Spectre.Console-powered inspector after the initial run. |
| `--summary-filter <name>` | Chooses which nodes appear in the final tree (e.g. `differences`, `left`, `right`, `errors`, `all`). Defaults to `differences`. |
| `--no-progress` | Suppresses the live progress panel that streams MB/s, files/s, totals, and pipeline counts. |
| `--fail-on <policy>` | Controls the exit code (`any`, `diff`, or `error`). |
| `--timeout <seconds>` | Aborts the run after the specified timeout. |

For the complete option set, run `fsequal compare --help`.

By default the CLI renders a live progress panel that reports left/right throughput in MB/s, files processed per second, total bytes read, and how many files remain in the pipeline. Use `--no-progress` to suppress the panel in CI logs.

## Examples

Compare two directories, ignore build output, and open the interactive inspector:

```bash
fsequal compare ./src ./baseline --ignore "**/bin/**" --ignore "**/obj/**" --interactive
```

Compare a directory against a baseline manifest with SHA-256 hashes and write JSON exports:

```bash
fsequal compare ./src --baseline artifacts/baseline.json --algo sha256 --json artifacts/report.json --summary artifacts/summary.json
```

Run in headless mode but still capture a Markdown export bundle defined in configuration:

```bash
fsequal compare ./src ./baseline --export markdown-bundle --no-progress
```
