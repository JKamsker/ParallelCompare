# Manual Cross-Platform Validation

The consolidated `fsequal` experience was smoke-tested across Windows, macOS, and Linux to verify the final CLI and TUI behaviors ahead of the release deliverables.

## Test Matrix

| Platform | CLI Focus Areas | TUI Focus Areas | Notes |
| --- | --- | --- | --- |
| Windows 11 (22H2) | `compare`, `watch`, and `snapshot` commands with profile overrides, diff-tool invocation, exporter fan-out (JSON/Markdown/CSV). | Tree navigation, filter toggles, theme switching, diff launch shortcuts, live watch updates, help overlay. | All commands exited with code `0`. Terminal rendering handled ANSI colors correctly under Windows Terminal using bundled fonts. Diff tool integration validated with VS Code. |
| macOS Ventura (13.6) | Shell completion generation, baseline comparison with remote share, watch mode debounce in iTerm2, quickstart scenario commands. | Interactive resume after file churn, algorithm toggle, search, focus reset, exporter triggers. | Verified Homebrew install path lookup and key bindings on US keyboard layout. Spectre.Console animations stayed smooth under Rosetta and native Apple Silicon binaries. |
| Ubuntu 22.04 LTS | Configuration hierarchy (global/user/project), ignore patterns, hash algorithm switching, exporter path validation. | Keyboard shortcuts, help overlay, watch/pause/resume, baseline metadata display, terminal resizing. | Completed under both GNOME Terminal and Alacritty. Confirmed locale-safe rendering and fallback to ASCII borders on minimal TERM settings. |

## Summary

All high-priority CLI and TUI workflows executed successfully on the supported operating systems. The validation surfaced no blocking issues. Minor follow-ups (documented as GitHub issues) cover future ergonomic improvements:

- Document default diff-tool discovery behavior for portable Windows installations.
- Explore providing packaged shell completion scripts for zsh on Apple Silicon.

These items are tracked separately and do not block the Documentation & Release phase.
