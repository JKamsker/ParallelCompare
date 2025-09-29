# Recommended Path to the Best fsEqual Implementation

## What the spec demands
- Deliver a multi-face fsEqual tool that exposes both a scripted CLI and a Spectre.Console-driven interactive TUI for directory comparisons, complete with live progress, configurable verbosity, ignore globs, multiple hash algorithms, profile/config support, and machine-readable exports.【F:docs/spec.md†L13-L64】【F:docs/spec.md†L94-L155】
- Provide advanced workflows beyond the base compare command, including snapshot/manifest support, a filesystem watch mode, external diff tooling, export pipelines, remote comparisons, and shell completions.【F:docs/spec.md†L232-L296】【F:docs/spec.md†L303-L352】【F:docs/spec.md†L374-L400】

## Assessment of the four candidate PR branches

### PR A — `codex/implement-plan-in-docs/spec.md`
**Strengths**
- Solid baseline `compare` command with Spectre.Console output, JSON/summary exporters, timeout handling, and profile-based configuration resolution that merges defaults, profiles, and CLI overrides.
- Produces detailed comparison results (differences, errors, summary metrics) and an interactive mode with export capabilities.

**Gaps**
- Lacks complementary commands (`watch`, `snapshot`, completions) and richer pipelines (no baseline snapshots, diff launchers, remote mode).
- Interactive view is list-based without the tree navigation, key bindings, or live session controls spelled out in the spec.

### PR B — `codex/implement-plan-in-docs/spec.md-ehgdxp`
**Strengths**
- Implements a comprehensive comparison pipeline with structured snapshots, rich Spectre.Console reporting, and an interactive TUI that matches the spec’s expectations: tree navigation via arrow keys, filtering, re-compare, algorithm toggling, exporting, and verbosity cycling.
- Comparison engine tracks detailed node statistics, supports hashing with cancellation, and yields data structures ready for visualization.

**Gaps**
- Only surfaces a single `compare` command; no watch/snapshot/completion support, and no profile/config ingestion.
- Export story is limited; JSON/summary writers exist but lack the multiple-format/export triggers described in the spec.

### PR C — `codex/implement-plan-in-docs/spec.md-fs5wxg`
**Strengths**
- Covers breadth of commands: compare, watch, snapshot, and completion generation, plus baseline handling and report exporters for JSON/summary.
- Adds configuration/profile loading, hash computation utilities, and a watch mode with filesystem debouncing.

**Gaps**
- Interactive experience is menu-driven and lacks the live tree/table drill-down, keyboard shortcuts, and progressive status panes outlined in the spec.
- Comparison model is flatter (no hierarchical node tracking), making advanced filters or diff viewer hooks harder to add. No diff-tool integration or remote compare primitives.

### PR D — `codex/implement-plan-in-docs/spec.md-jzl3qc`
**Strengths**
- Most complete command surface: compare, watch, snapshot, completion; supports baselines, fail-on rules, verbosity control, and Spectre.Console reporting with JSON/Summary exports.
- Comparison core includes snapshots, hashing utilities, manifest serialization, and watch debouncing. Snapshot command can both create and validate baselines.
- Interactive session offers filtering, browsing, exporting, and verbosity toggles aligned with spec shortcuts (though via menu prompts rather than tree navigation).

**Gaps**
- No configuration/profile loader despite `ComparisonOptions` carrying `Profile`/`ConfigPath`—CLI ignores those inputs.
- Interactive UI stops at list/browse views, missing the split-pane tree/table navigation and live pipeline controls (pause, re-queue, algorithm toggle) highlighted in the spec.
- Remote comparison, diff-tool launchers, and parallel exporter fan-out remain unimplemented.

## Recommended baseline and merge strategy
1. **Adopt PR D as the foundation.** It already supplies the richest command set, baseline & watch support, manifest serialization, and export hooks, reducing the amount of plumbing work needed to satisfy the spec’s breadth.
2. **Port PR A’s configuration resolution layer.** Wire its profile/config loader and resolved settings into PR D’s `CompareSettings`/`ComparisonOptions` so profiles, defaults, and CLI overrides behave exactly as specified.
3. **Replace/enhance PR D’s interactive session with PR B’s tree-driven TUI.** PR B already implements the arrow-key navigation, status panes, filter cycling, algorithm toggling, re-compare triggers, and export shortcuts envisioned by the spec. Adapt PR B’s `InteractiveSession` (and supporting data structures) to consume PR D’s richer comparison model.
4. **Borrow PR C’s additional exporters and watch ergonomics as needed.** Its report exporter supports multiple formats and its watch command handles debounce + initial compare; integrate any superior UX details into the PR D baseline.

## Implementation plan to reach spec parity
1. **Merge PR D code into the working branch** (or rebase onto it) as the primary code drop.
2. **Integrate configuration profiles**:
   - Add PR A’s config loader & resolved settings pipeline.
   - Update compare/watch/snapshot commands to honor defaults, profiles, and CLI overrides.
3. **Upgrade the interactive TUI**:
   - Introduce PR B’s tree/table explorer with keyboard shortcuts (`↑/↓/←/→`, `F`, `A`, `R`, `E`, `L`, `P`, `Q`, `?`).
   - Ensure exports, re-queueing, and live status updates tie back into PR D’s comparison engine.
4. **Unify reporting/exporting**:
   - Consolidate JSON/summary writers (PR A & D) and add CSV/Markdown fan-out similar to PR C.
   - Hook exporters into both CLI and interactive flows per spec (`E` key, multiple formats).
5. **Extend advanced workflows**:
   - Flesh out diff-tool launching (`--diff-tool` flag + interactive `D` key) leveraging PR D’s metadata.
   - Scaffold remote compare stubs (e.g., SSH URL parsing) to align with the spec’s future-facing section, even if implemented as TODOs with graceful messaging.
6. **Polish watch & snapshot modes**:
   - Combine PR C’s debounce UX with PR D’s watch loop.
   - Ensure snapshot compare honors the same exporters, exit codes, and verbosity as the main compare command.
7. **Document and test**:
   - Update help text/README to describe all commands and key bindings.
   - Add unit/functional tests for config resolution, hash comparison, export generation, and watch debounce behavior.

## Remaining spec items to plan for after merging
- Remote comparisons over SSH/streamed hashes still need full implementation hooks.【F:docs/spec.md†L323-L345】
- External diff viewer integration (`--diff-tool`, interactive `D`) must be designed and wired up.【F:docs/spec.md†L303-L321】
- Parallel exporter fan-out (`--json` + `--csv` + `--html`) and custom exporter discovery remain outstanding.【F:docs/spec.md†L347-L372】
- Theming, telemetry, and community tooling (GitHub Action, VS Code extension) can follow once the core CLI/TUI stabilizes.【F:docs/spec.md†L355-L420】