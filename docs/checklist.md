# ParallelCompare Integration Checklist

This checklist tracks the work required to deliver the consolidated implementation described in `docs/consolidated-implementation.md`.

## Phase 1 – Foundation Merge
- [x] Import jzl3qc command modules (`compare`, `watch`, `snapshot`, `completion`).
- [x] Integrate configuration/profile resolver from base spec implementation.
- [x] Standardize shared option set and exit codes across commands.

## Phase 2 – Tree Model Integration
- [x] Replace flat comparison records with hierarchical `ComparisonNode` data model.
- [x] Adapt exporters and summaries to consume the tree structure.
- [x] Implement adapters to stream engine updates into tree nodes for live updates.

## Phase 3 – Interactive Experience Upgrade
- [x] Embed Spectre.Console TUI from ehgdxp implementation.
- [x] Wire keyboard shortcuts (navigation, filters, algo toggle, exports, diff, pause/resume, help).
- [x] Ensure interactive mode respects configuration defaults (theme, filter, verbosity).

## Phase 4 – Watch & Snapshot Enhancements
- [ ] Merge FileSystemWatcher pipeline with new engine and TUI refresh cycle.
- [ ] Support `--baseline` comparisons without requiring physical right-hand directory.
- [ ] Surface watch status and baseline metadata in both CLI and TUI outputs.

## Phase 5 – Testing & Polishing
- [ ] Author unit tests for configuration resolution, exporter selection, and diff-tool invocation.
- [ ] Create integration tests for compare/watch/snapshot workflows.
- [ ] Conduct manual cross-platform validation (Windows, macOS, Linux) for TUI and CLI flows.

## Documentation & Release
- [x] Produce consolidated implementation plan.
- [ ] Update user documentation and quickstart guides once implementation stabilizes.
- [ ] Prepare release notes and onboarding materials for the combined feature set.

