# Manual Cross-Platform Validation

The consolidated ParallelCompare experience was smoke-tested on the three supported desktop platforms to ensure parity between the CLI and Spectre-based TUI flows.

## Windows 11 (22H2)
- **CLI**: `parallelcompare compare C:\Samples\left C:\Samples\right --threads 8 --baseline baselines/windows.json`
  - Verified summary output, ANSI color fallback, and diff-tool invocation (`--diff-tool tortoisegitproc`).
  - Confirmed exporters (JSON, JUnit) write reports to the working directory with Windows path separators.
- **TUI**: `parallelcompare compare C:\Samples\left C:\Samples\right --interactive`
  - Navigation, filtering, algorithm toggle, export shortcuts, help overlay, and diff launch behaved as documented.
  - Watch refresh indicator reflected changes from an appended file within 2 seconds.

## macOS 13 (Ventura)
- **CLI**: `parallelcompare compare ~/Projects/left ~/Projects/right --watch --no-progress`
  - Observed watch loop handling case-sensitive and case-insensitive file systems, with baseline metadata surfaced in headers.
  - `--diff-tool opendiff` integration launched FileMerge successfully and returned control to the CLI after exit.
- **TUI**: `parallelcompare compare ~/Projects/left ~/Projects/right --interactive --theme light`
  - Verified keyboard shortcuts, snapshot creation (`s`), exporter bindings, and pause/resume workflow (`space`).
  - High contrast theme rendered correctly in both terminal.app and iTerm2.

## Ubuntu 22.04 LTS
- **CLI**: `parallelcompare compare ~/src/left ~/src/right --baseline baselines/linux.json --json reports/out.json`
  - Ensured POSIX permission differences surface as dedicated nodes and respect ignore rules.
  - JSON and Markdown exporters succeeded under non-English locale.
- **TUI**: `parallelcompare compare ~/src/left ~/src/right --interactive`
  - Verified `?` help overlay, filters, status bar watch indicators, and diff-tool integration with `meld`.
  - Confirmed application handles narrow terminals (80 columns) without layout breakage.

## Key Outcomes
- CLI and TUI experiences are aligned across Windows, macOS, and Linux.
- Exporters, diff integrations, and watch mode operate consistently on all platforms.
- No release-blocking regressions were observed; minor cosmetic notes tracked in the issue backlog (`docs/consolidated-implementation.md` known issues section).
