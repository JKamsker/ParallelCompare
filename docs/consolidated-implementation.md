# FsEqual Consolidated Implementation Plan

## Purpose

This document merges the strongest ideas from the four proof-of-concept branches evaluated in `docs/Eval-2.md` into a single, coherent implementation strategy for the `fsEqual` tool. The goal is to satisfy the requirements in `docs/spec.md` by unifying:

- The **command surface, watch mode, and snapshot/baseline workflow** of `codex/implement-plan-in-docs/spec.md-jzl3qc`.
- The **rich Spectre.Console-driven TUI** from `codex/implement-plan-in-docs/spec.md-ehgdxp`.
- The **configuration/profile system and validation pipeline** from `codex/implement-plan-in-docs/spec.md`.
- The **export plumbing and supporting utilities** found in `codex/implement-plan-in-docs/spec.md-fs5wxg`.

## Architectural Overview

The consolidated solution is organized into four layers. Each layer borrows the most robust portion of a prior implementation and adapts it to satisfy the full spec.

1. **Interface Layer**
   - *Commands*: `compare`, `watch`, `snapshot`, and `completion` (from jzl3qc).
   - *Interactive UI*: Spectre.Console TUI with tree navigation, filtering, exports, and keyboard shortcuts (from ehgdxp).
   - *Progress & Logging*: Verbosity-aware console logging coupled with live progress widgets.

2. **Configuration Layer**
   - *Source Hierarchy*: CLI args → profile → config file → defaults (from base spec & fs5wxg).
   - *Profiles*: Named presets for hash mode, ignores, exporters, diff tools.
   - *Validation*: Centralized `SettingsValidator` enforcing path existence, option compatibility, and feature gating.

3. **Comparison Engine**
   - *Pipelines*: Enumerator → Scheduler → Worker Pool → Aggregator (from jzl3qc core).
   - *Data Model*: Hierarchical `ComparisonNode` tree with metadata for directories and files (from ehgdxp).
   - *Hashers*: CRC32 (default), MD5, SHA-256, XXH64 via pluggable providers.
   - *Diff Integration*: Command-level `--diff-tool` plus interactive `D` shortcut invoking external tools.

4. **Outputs & Automation**
   - *Reports*: JSON, summary JSON, CSV, Markdown exports with shared schema (from fs5wxg + ehgdxp exporters).
   - *Snapshots*: Persisted manifest of hashes and metadata, reused for baseline comparisons.
   - *Watch Mode*: FileSystemWatcher driving debounced re-comparisons; integrates with TUI refresh loop.
   - *Completion*: Spectre command app provides shell completion generation.

## Feature Integration Details

### CLI Commands
- Reuse the jzl3qc command shell to ensure parity with the spec.
- Merge option definitions to include configuration inputs (`--config`, `--profile`) and diff tooling switches.
- Standardize exit codes (0 equal, 1 diff, 2 error) across all modes.

### Configuration & Profiles
- Adopt the resolver pipeline from the base spec implementation: parse CLI, hydrate from config, apply profile overrides, produce `ResolvedCompareSettings`.
- Expose configuration-driven defaults for exporters, ignore patterns, watch debounce durations, and TUI preferences (theme, initial filter).

### Comparison Engine & Data Model
- Embed the hierarchical `ComparisonNode` structure from the TUI POC inside the multi-threaded engine of jzl3qc.
- Provide adapters to translate engine events into node updates so the TUI can update in real time.
- Ensure quick mode (mtime/size) and hash mode share the same tree representation.

### Interactive Experience
- Integrate the full key-bind set from ehgdxp (navigation, filter, algo toggle, re-run, export, verbosity, pause, help, quit).
- Extend with watch-mode awareness: show live badges when filesystem events trigger re-runs; allow manual pause/resume from the UI.
- Support partial re-hashing triggered by selecting a node and pressing `A`.

### Watch Mode
- Combine FileSystemWatcher logic from jzl3qc with the UI refresh pipeline from ehgdxp.
- Provide CLI-only operation (console summary) and interactive integration (`--interactive --watch`).
- Debounce events to avoid redundant runs; surface status in TUI footer.

### Snapshot & Baseline Workflow
- Use jzl3qc manifest serializer for `fsequal snapshot`.
- Allow `fsequal compare <path> --baseline baseline.json` to bypass filesystem validation for the right-hand side, supporting offline comparisons.
- Display baseline metadata within the TUI when active.

### Export Pipeline
- Consolidate exporters into a shared `IReportExporter` interface with implementations for JSON, summary JSON, CSV, and Markdown.
- Allow multiple exporters per run; support config-driven exporter defaults.
- Integrate interactive `E` key to invoke the same exporter infrastructure.

### Logging & Diagnostics
- Maintain verbosity-aware logger with scopes for engine stages.
- Provide structured error messages for config issues, missing files, unsupported diff tools, or hash provider failures.
- Capture telemetry for benchmarking hooks (optional). 

### Extensibility Hooks
- Plugin model for hash providers and exporters, auto-discovered via dependency injection.
- Theme customization for TUI via configuration.
- Extension points for remote compare (future roadmap) left as interfaces.

## Implementation Phases

1. **Foundation Merge**
   - Import jzl3qc command shell and engine.
   - Overlay configuration resolver from base spec implementation.
   - Ensure compare/watch/snapshot commands compile with new settings model.

2. **Tree Model Integration**
   - Replace flat comparison results with `ComparisonNode` tree.
   - Adjust exporters and CLI summary to consume the hierarchical model.

3. **Interactive Experience Upgrade**
   - Integrate ehgdxp Spectre UI, binding it to the new engine adapters.
   - Wire up exporter prompts and diff tool commands.

4. **Watch & Snapshot Enhancements**
   - Bridge FileSystemWatcher events into interactive session updates.
   - Validate baseline workflows within new configuration pipeline.

5. **Testing & Polishing**
   - Unit tests for settings resolution, exporter outputs, and diff launching.
   - Integration tests for compare/watch/snapshot commands.
   - Manual verification of TUI workflows on Windows, macOS, Linux.

## Open Questions
- How to harmonize progress reporting across CLI and TUI without double-rendering?
- Should exporter plugins run in parallel or sequentially to preserve log readability?
- What is the default behavior when both watch mode and interactive algorithm toggling queue work concurrently?

## Success Metrics
- All commands available and stable.
- TUI supports full navigation and live refresh.
- Configured defaults produce consistent results across CLI, TUI, watch, and snapshot modes.
- Export outputs conform to shared schema and pass JSON schema validation.
- Baseline comparisons and diff tool invocations succeed across platforms.

## Next Steps
- Execute Phase 1 tasks (see `docs/checklist.md`).
- Schedule architecture review once tree model integration is complete.
- Begin drafting user documentation and onboarding materials post-MVP.

