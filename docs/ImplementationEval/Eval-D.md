# Plan for Selecting the Best fsEqual Implementation

## Evaluation Overview
To decide which pull request implementation best fulfills the `docs/spec.md` roadmap, I compared all four candidate branches. Each entry below highlights the major strengths and omissions relative to the spec.

| Branch | Highlights | Significant Gaps |
| --- | --- | --- |
| `codex/implement-plan-in-docs/spec.md` | Robust option parsing with profile/config resolution and helpful validation pipeline; JSON/summary export handled centrally; console logger abstraction for verbosity-aware output. | Only `compare`/`completion` commands implemented; interactive mode is a simple list without tree navigation, re-run, or algorithm switching; no watch or snapshot support. |
| `codex/implement-plan-in-docs/spec.md-ehgdxp` | Deep Spectre.Console TUI that matches the spec’s tree/table workflow (filters, re-run, on-the-fly hash changes, export prompt); comparison core cleanly models snapshots/hashes. | Lacks watch/snapshot/ completion commands; no configuration/profile system; CLI hard-validates real directories so baseline/snapshot workflows cannot run; progress/export integration outside interactive mode is limited. |
| `codex/implement-plan-in-docs/spec.md-fs5wxg` | Full command surface (`compare`, `watch`, `snapshot`, `completion`); baseline comparison and config/profile loader supported; reporting/export pipeline writes JSON & markdown/csv; watch mode debounced with live summaries. | Interactive explorer is menu-driven without live tree or keyboard navigation; comparison pipeline mixes concerns (enumeration, comparison, reporting) making extension harder; error handling around config/baseline resolution is thin. |
| `codex/implement-plan-in-docs/spec.md-jzl3qc` | Implements compare/watch/snapshot/completion, JSON & summary exports, baseline validation, cancellation handling, and watch debounce; comparison engine cleanly separates file/directory/baseline diffs; CLI surface closely mirrors spec (timeouts, fail-on, no-progress, interactive). | No configuration/profile loader; interactive mode offers menus but lacks tree-based navigation and in-session re-compare; diff-tool integration and advanced exporters absent. |

## Recommendation
Use `codex/implement-plan-in-docs/spec.md-jzl3qc` as the foundation—it already covers the required command set, reporting surface, baseline support, and watch workflow while keeping the comparison core modular. Augment it with the strongest ideas from the other branches:

* Adopt the configuration/profile resolution pipeline from `codex/implement-plan-in-docs/spec.md` so CLI users can layer defaults, profiles, and command-line overrides.
* Merge the rich interactive TUI from `codex/implement-plan-in-docs/spec.md-ehgdxp` to deliver the spec’s drill-down browsing, filtering, and on-demand rehash features.
* Reuse the flexible exporters (markdown/csv alongside JSON) and config-driven baseline helpers from `codex/implement-plan-in-docs/spec.md-fs5wxg` where they exceed the base branch.

## Integration Plan
1. **Configuration & Profiles** – Port `ConfigLoader`/`ResolvedCompareSettings` style resolution into the recommended base so every command (compare/watch/snapshot) can pull defaults/profiles from `fsequal.config.json`. Ensure validation surfaces clear errors and apply merged settings consistently across commands.
2. **Interactive Mode Upgrade** – Replace the base branch’s menu-only session with the tree/table experience from `spec.md-ehgdxp`, wiring it to the base branch’s `ComparisonResult` model and enabling re-run, algorithm toggling, filtering, exporter hooks, and verbosity cycling as per the spec.
3. **Exporter Harmonization** – Combine the base branch’s JSON/summary writing with the markdown/csv exporters from `spec.md-fs5wxg`, exposing them both via CLI (`--json/--summary`) and interactive `Export` actions.
4. **Watch & Snapshot Consistency** – Align watch/snapshot commands to share configuration resolution and exporter pipeline. Ensure watch mode respects debounce, re-runs compare with merged options, and snapshot mode can both create baselines and compare against them.
5. **Diff Tool Hooks & Future Work** – Stub integration points (e.g., `--diff-tool`, remote compare placeholders) so the code structure anticipates remaining spec items, referencing the best patterns observed (progress reporting from `spec.md`, tree refresh from `spec.md-ehgdxp`).
6. **Documentation & Help** – Update command help and README snippets to reflect the combined feature set, leveraging Spectre.Console’s example support for clarity.

Following this plan keeps the most complete feature surface (`spec.md-jzl3qc`), while layering in the standout configuration, interactive, and exporter capabilities showcased across the other pull requests.