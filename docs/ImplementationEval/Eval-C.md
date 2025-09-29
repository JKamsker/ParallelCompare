# ParallelCompare Implementation Evaluation and Plan

## Method
- Fetched the four candidate branches (`codex/implement-plan-in-docs/spec.md*`) from the upstream
  repository.
- Reviewed the structure of each branch by inspecting the command surface, core comparison
  services, and interactive/TUI layers.
- Compared each branch to the expectations laid out in `docs/spec.md`, with a focus on CLI
  coverage, interactive experience, extensibility (config, profiles, exports), and advanced
  workflows (watch mode, snapshots, baselines).

## Candidate Summaries

### `codex/implement-plan-in-docs/spec.md`
- Uses `ResolvedCompareSettings` to merge CLI arguments, config files, and profiles before building
  `ComparisonOptions`, giving strong coverage of the spec’s configuration story.
- `DirectoryComparator` performs parallel hashing for all supported algorithms and emits concise
  Spectre panels/tables, but it only gathers a flat difference list (no tree model).
- Interactive mode is intentionally lightweight: a single screen with filter cycling, export, and a
  help panel. It lacks directory navigation, live refresh, algorithm toggling, or deep drill-down.
- No watch/snapshot commands, baselines, or diff-tool integrations are present.

### `codex/implement-plan-in-docs/spec.md-ehgdxp`
- Builds a rich `ComparisonResult` tree via `ComparisonService`, powering an interactive explorer
  with arrow-key navigation, expand/collapse, filtering, algorithm swaps, refresh, and exports.
- CLI output includes progress spinners, structured summaries, and JSON/CSV/Markdown export
  options via `ReportExporter`.
- Lacks configuration/profile loading; `--config`/`--profile` options are parsed but never applied.
- Only the `compare` and `completion` commands are implemented—no watch mode, snapshot, or
  baseline support.

### `codex/implement-plan-in-docs/spec.md-fs5wxg`
- Provides the broadest CLI surface: compare, watch, snapshot, and completion commands. Baseline
  comparisons are supported by loading snapshot files, and the watch command re-runs comparisons on
  filesystem events.
- Configuration/profile loading is implemented through `ConfigurationLoader`, enabling reuse of
  ignore patterns, defaults, and report destinations.
- Interactive experience is menu-driven (`InteractiveExplorer`) with filtering and export options
  but lacks the live tree navigation and keyboard workflows called out in the spec.
- Comparison data structures focus on flat lists of `PathComparison` records, so extending to a
  hierarchical TUI would require additional modeling work.

### `codex/implement-plan-in-docs/spec.md-jzl3qc`
- Offers a mature command lineup: `compare`, `watch`, `snapshot`, and `completion`, plus baseline
  comparisons baked into `DirectoryComparer`.
- `CompareReporter` delivers verbose console output with verbosity controls and JSON/summary export
  hooks, aligning well with the CLI requirements.
- Interactive mode (`InteractiveCompareSession`) gives menu-based browsing, filtering, and export
  (including JSON/CSV/Markdown) with verbosity toggles, yet it stops short of the tree-based
  navigation and hotkeys enumerated in the spec.
- Configuration/profile handling is stubbed at the option level but not implemented, and there is no
  reusable report writer for non-interactive exports.

## Recommended Path Forward
1. **Adopt `codex/implement-plan-in-docs/spec.md-jzl3qc` as the foundation.** It already ships the
   widest set of commands (compare, watch, snapshot), baseline support, and robust console reporting,
   making it the closest end-to-end fit for the spec’s CLI surface.
2. **Port the configuration/profile pipeline from `codex/implement-plan-in-docs/spec.md-fs5wxg`.**
   Introduce its `ConfigurationLoader`-style approach (or the equivalent `ResolvedCompareSettings`
   workflow from `codex/implement-plan-in-docs/spec.md`) so `--config`/`--profile` flags hydrate
   defaults, ignore globs, output paths, and interactive toggles before comparisons start.
3. **Replace the menu-based TUI with the tree explorer from `codex/implement-plan-in-docs/spec.md-ehgdxp`.**
   Reuse its `ComparisonNode` model and `InteractiveSession` navigation to satisfy the spec’s keyboard
   shortcuts (arrow navigation, expand/collapse, filters, live algorithm swapping, refresh, export).
   Wire the richer tree into the comparison pipeline so watch/snapshot/baseline flows can feed the
   same interactive experience.
4. **Consolidate reporting/export helpers.** Keep `CompareReporter` for console output, but adopt the
   flexible `ReportExporter` from `codex/implement-plan-in-docs/spec.md-ehgdxp` to unify JSON/CSV/
   Markdown exports across CLI and interactive flows, ensuring interactive mode warns when CLI export
   flags are ignored (as in the spec branch).
5. **Harden concurrency and hashing primitives.** Leverage the proven parallel hashing logic from
   `codex/implement-plan-in-docs/spec.md` (with CRC32/MD5/SHA256/XXH64) inside the consolidated
   comparer so both tree rendering and snapshot/baseline workflows share the same, tested core.
6. **Round out polish items.** Add diff-tool launch hooks, ensure watch mode surfaces live status in
   the TUI, and integrate verbosity-aware logging/trace output by merging the best practices from the
   evaluated branches.

Following this plan yields a combined implementation that keeps the comprehensive command coverage of
`spec.md-jzl3qc`, enriches it with configuration flexibility, delivers the full-featured interactive
experience described in the spec, and shares one consistent export/logging pipeline.