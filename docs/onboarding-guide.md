# Onboarding Guide

Welcome to ParallelCompare! This guide helps new users ramp up quickly on the consolidated CLI + TUI experience.

## 1. Install the Tool

```bash
dotnet tool install --global FsEqual.Tool
```

Confirm installation:

```bash
fsequal --version
```

## 2. Learn the Workflow

1. Run `fsequal compare left right --interactive` to open the tree explorer.
2. Press `?` for the in-app cheat sheet.
3. Toggle filters (`m`, `s`, `u`) and launch diffs (`d`) to review mismatches.

## 3. Configure Projects

- Create `.fsequal/config.json` and define ignores, hash algorithms, and exporter bundles.
- Store reusable configurations under `profiles` so CI/CD and local workflows stay consistent.
- Use `fsequal compare --profile ci` or `fsequal watch --profile watch` to apply shared defaults.

## 4. Watch Changes Live

```bash
fsequal watch src main --interactive --debounce 250ms
```

- Pause with `space`, resume with `r`.
- Exporters update automatically after each run.

## 5. Capture Baselines

```bash
fsequal snapshot create baselines/api.json --source artifacts/api
fsequal snapshot compare baselines/api.json --target artifacts/api-new --interactive
```

Store snapshots alongside release artifacts so regressions are easy to detect.

## 6. Automate in CI

```yaml
- name: Compare candidate vs baseline
  run: |
    fsequal compare artifacts/golden artifacts/candidate \
      --no-interactive \
      --profile ci \
      --export json=artifacts/report.json \
      --export markdown=artifacts/summary.md
```

## 7. Explore Further

- [User Guide](user-guide.md)
- [Manual Cross-Platform Validation](manual-validation.md)
- [Release Notes](release-notes.md)

