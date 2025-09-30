# FsEqual Release Notes

## v1.0.0

### Highlights

- **Unified command surface** – `compare`, `watch`, `snapshot`, and `completion` commands now share a consistent option model and exit codes.
- **Tree-first interactive explorer** – Spectre.Console TUI delivers keyboard-driven navigation, filters, search, and live exporter triggers.
- **Watch mode improvements** – Debounced filesystem monitoring feeds directly into the tree view with pause/resume controls and baseline metadata.
- **Snapshot & baseline workflows** – Create baselines, verify candidates, and reuse recorded metadata across releases.
- **Exporter fan-out** – JSON, Markdown, and CSV exporters can run together, with diff-tool integration for deep dives.

### Getting Started

See the [User Guide](user-guide.md) for installation instructions and quickstart scenarios. Highlights include:

- Launch `fsequal compare A B --interactive` for the TUI.
- Enable watch mode with `fsequal watch A B --interactive`.
- Record baselines via `fsequal snapshot create`.

### Compatibility

- Requires .NET 8 runtime.
- Supports Windows, macOS, and Linux. Manual validation across all platforms confirmed parity for CLI and TUI flows (see [Manual Cross-Platform Validation](manual-validation.md)).

### Known Issues

- Portable Windows environments may require `--diff` to reference the absolute path to the diff tool.
- Pre-built shell completions for zsh on Apple Silicon will be published in a follow-up update.

### Upgrading

```bash
dotnet tool update --global FsEqual.Tool
```

Breaking changes since the previous implementation:

- Command-line flags align with the consolidated configuration model; legacy aliases are removed.
- Exporter configuration now requires explicit `type=path` syntax (e.g., `json=out/report.json`).

