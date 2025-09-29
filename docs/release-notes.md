# Release Notes

## ParallelCompare 1.0.0

### Highlights
- **Unified comparison engine** that powers both CLI and Spectre.Console TUI experiences.
- **Hierarchical results tree** with live updates and watch mode integration.
- **Baseline workflows** enabling snapshot creation, drift detection, and metadata diffing.
- **Exporter suite** (JSON, Markdown, JUnit) with configurable defaults per profile.
- **Diff tool integrations** for popular viewers across Windows, macOS, and Linux.
- **Accessibility & theming improvements** including high-contrast and light modes.

### User experience updates
- Quickstart commands now cover interactive usage, watch mode, and baseline comparisons.
- Help overlays and CLI `--help` output align on key bindings and option descriptions.
- Notifications signal exporter success/failure and diff tool launches in the TUI.

### Developer & operations updates
- Consolidated configuration hierarchy across machine, user, repository, and CLI scopes.
- Snapshot format stabilized for forward compatibility with future releases.
- Automated release workflow publishes binaries, container images, and checksums.

### Known issues
- Terminal resizing below 80 columns may truncate status bar messaging.
- External diff tools inherit the parent process locale; override via wrapper script if necessary.
- See `docs/consolidated-implementation.md` for the complete backlog and mitigation plans.

### Onboarding checklist
1. Install the CLI from the latest GitHub release (`dotnet tool install --global ParallelCompare`).
2. Run `parallelcompare compare left right --interactive` to explore the TUI.
3. Configure preferred exporters and diff tools in `~/.config/parallelcompare/config.json`.
4. Capture a baseline snapshot before migrating critical environments.
5. Review `docs/user-guide.md` and the Quickstart cheatsheet for advanced scenarios.

### Communication plan
- Publish launch blog highlighting engine unification, TUI upgrades, and watch enhancements.
- Share release notes across GitHub Releases, project discussions, and the community newsletter.
- Update onboarding materials (Quickstart cheatsheet, migration notes) with the latest guidance.
