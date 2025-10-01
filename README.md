# FsEqual

FsEqual is a fast, Spectre.Console-powered comparison tool that inspects two directory trees (or a tree against a saved baseline) in parallel. The experience unifies a rich interactive TUI, automation-friendly CLI, snapshotting, and export pipelines so that changes are always clear—whether you are shipping to production or reviewing a pull request.

## Highlights

- ⚡️ **High-throughput comparisons** powered by parallel workers and configurable hash algorithms.
- 🧭 **Interactive inspector** with filtering, theming, diff tooling, and live watch mode.
- 🗂️ **Baselines & snapshots** to freeze known-good trees and validate future changes.
- 📦 **Portable exports** including JSON, CSV, Markdown, and baseline manifests for CI/CD.
- 🔌 **Extensible configuration** via profiles, ignore patterns, diff tool integration, and exporters.

## Install

```bash
# Install as a global .NET tool
dotnet tool install --global FsEqual.Tool

# Update to the latest release
dotnet tool update --global FsEqual.Tool
```

For isolated scenarios, publish a self-contained binary:

```bash
dotnet publish src/FsEqual.App -c Release -r <rid>
```

## Quick start

```bash
# Compare two directories with hashing and interactive mode enabled
fsequal compare ./src ./baseline --algo sha256 --interactive

# Monitor directories continuously and rerun on change
git checkout main
fsequal watch ./src ./baseline --debounce 500

# Capture a baseline manifest for regression testing
fsequal snapshot ./src --output artifacts/baseline.json

# Validate configuration without running the engine
fsequal compare ./src ./baseline --dry-run

# Produce exports silently for automation
fsequal compare ./src ./baseline --quiet --summary artifacts/summary.json
```

## Command reference

| Command | Description |
| --- | --- |
| [`compare`](docs/command-compare.md) | Compare two directories or a directory against a baseline. |
| [`watch`](docs/command-watch.md) | Continuously compare inputs and refresh on file changes. |
| [`snapshot`](docs/command-snapshot.md) | Capture a baseline manifest representing the current tree. |
| [`completion`](docs/command-completion.md) | Generate shell completion scripts. |

## Documentation

- [User guide](docs/user-guide.md)
- [Onboarding guide](docs/onboarding-guide.md)
- [Manual validation notes](docs/manual-validation.md)
- [Release notes](docs/release-notes.md)
- [Specification](docs/spec.md)

## Contributing

1. Clone the repository and install the .NET SDK 9.0 or newer.
2. Restore dependencies and run the test suite:
   ```bash
   dotnet test
   ```
3. Submit pull requests with updated documentation and passing tests.

## License

FsEqual is licensed under the MIT License. See [LICENSE](LICENSE) for details.
