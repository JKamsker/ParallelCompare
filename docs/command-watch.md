# `fsequal watch`

The `watch` command keeps directories under observation and reruns comparisons whenever files change. It shares most options with `compare` but adds controls for throttling and a richer interactive refresh workflow.

## Usage

```bash
fsequal watch <left> [right]
```

The `<left>` and optional `[right]` arguments match the semantics of `compare`. A baseline can also be supplied via `--baseline` when the right-hand tree is omitted.

## Additional options

| Option | Description |
| --- | --- |
| `--debounce <milliseconds>` | Waits for the specified quiet period before triggering a rerun (default `750`). |

All other options from [`compare`](command-compare.md) are available, including exporters, diff tool integration, and interactive mode.

## Workflow

1. The initial comparison runs immediately and prints a summary tree.
2. File system watchers observe both trees (or the tree and baseline) and queue reruns using the debounce interval.
3. In interactive mode the session displays live status banners, supports pausing/resuming with the spacebar, and can queue background exports while you inspect differences.

## Examples

Run watch mode interactively with faster refreshes:

```bash
fsequal watch ./src ./baseline --debounce 300 --interactive
```

Monitor a directory against a baseline in headless mode but still emit reports:

```bash
fsequal watch ./src --baseline artifacts/baseline.json --json artifacts/watch-report.json --summary artifacts/watch-summary.json
```
