# fsEqual — Full Plan (CLI + TUI with Spectre.Console)

## 0) High-level

* **Purpose**: Fast, multi-threaded, * "Deep dive a directory": select folder → `Enter` to expand → choose file → view both sides' metadata.
* "Re-hash only this dir with SHA-256": select dir → `A` → choose `sha256` → re-queue subset.

**Note**: `--json`, `--summary`, and `--no-progress` flags are ignored in interactive mode. Export functionality is available via the `E` key within the interactive interface.

---nel-based folder comparison.
* **Faces**:

  * **CLI** for automation/CI and scripted runs.
  * **TUI** (terminal UI) for interactive exploration: progress bars, live tables, drill-down, filters.
* **Hash algos**: `crc32` (default), `md5`, `sha256`, `xxh64` (optional plugin).
* **Logging**: Info/Warn/Error (+ trace/debug), verbosity configurable. Structured output, optional JSON.

---

## 1) Installation & Invocation

* **Dotnet tool**:
  `dotnet tool install -g FsEqual.Tool` → invokes `fsequal`
* **Single-file binaries** (Win/macOS/Linux): unzip → `./fsequal`
* **Help**: `fsequal --help` (Spectre rich help), `fsequal compare --help`

---

## 2) Modes

### A) Compare Command (CLI & Interactive)

Primary command: `compare`

```
fsequal compare <left> <right>
  [-t|--threads <int>]
  [-a|--algo <crc32|md5|sha256|xxh64>]
  [-m|--mode <quick|hash>]
  [-i|--ignore <glob>...]             # repeatable
  [--case-sensitive] [--follow-symlinks]
  [--mtime-tolerance <seconds>]       # only in quick mode
  [-v|--verbosity <trace|debug|info|warn|error>]
  [--json <path>]                     # detailed machine-readable report
  [--summary <path>]                  # small summary JSON
  [--no-progress]                     # disable live progress bars
  [--interactive]                     # launch interactive TUI mode
  [--timeout <seconds>]
  [--fail-on <any|diff|error>]        # maps to exit codes
  [--profile <name>] [--config <file>]
```

**Exit codes** (non-interactive mode only)

* `0` Equal
* `1` Differences found (no fatal errors)
* `2` Runtime error / aborted (IO error, cancellation, etc.)

**Typical runs** (non-interactive)

* Fast sanity check, default threads & algo:
  `fsequal compare ./A ./B`
* Fully hashed with 8 workers, ignore build junk:
  `fsequal compare ./A ./B -t 8 -a crc32 -m hash -i "**/bin/**" -i "**/obj/**"`
* Quick mode with mtime slop (2s):
  `fsequal compare ./A ./B -m quick --mtime-tolerance 2`
* Machine output for CI:
  `fsequal compare ./A ./B --json out/report.json --summary out/summary.json --no-progress -v warn`
* Interactive exploration:
  `fsequal compare ./A ./B --interactive`

**CLI Output UX (Spectre.Console)** (non-interactive)

* Live **progress**: tasks for Enumerate, Queue, Hashing, Aggregation.
* Final **panel** with totals: files compared / equal / diffs / missing / errors / duration.
* **Table** of differences (paginated or truncated with “show more” hint), columns:

  * Type (MissingLeft/MissingRight/TypeMismatch/SizeMismatch/HashMismatch)
  * Path (relative)
  * Size(L/R)
  * Algo (if hashed)
* Respect `--verbosity` for how much detail is printed.

---

### B) Interactive Mode (--interactive)

When `--interactive` flag is used with the `compare` command, the tool launches an interactive TUI interface.

```
fsequal compare <left> <right> --interactive [other options...]
```

All comparison options (`-t`, `-a`, `-m`, `-i`, etc.) work the same as non-interactive mode.

**Interactive Layout (Spectre.Console)**

* **Header**: left/right paths, algo, mode, workers, elapsed.
* **Left pane (Tree)**: directory tree with status glyphs:

  * ✓ equal, ! diff, – missing, ? error
* **Right pane (Table)**: details for selected node:

  * For dirs: child summary (counts by status) + last few diffs
  * For files: type/size/mtime/hash (if available), status reason
* **Footer / keybinds**:

  * `↑/↓/←/→` navigate
  * `F` filter (status/type/path)
  * `A` toggle algo on-the-fly (re-queue selected subset)
  * `R` re-compare selection
  * `E` export current view (`json/csv/markdown`)
  * `L` cycle verbosity
  * `P` pause/resume pipeline
  * `Q` quit, `?` help
* **Live progress** bar cluster at top or bottom; errors appear in a collapsible log panel.

**Interactive Workflows**

* “Just show me diffs”: start → press `F` → pick `Diff!=None` → browse.
* “Deep dive a directory”: select folder → `Enter` to expand → choose file → view both sides’ metadata.
* “Re-hash only this dir with SHA-256”: select dir → `A` → choose `sha256` → re-queue subset.

---

## 3) Comparison Semantics

* **Exists?** Missing on either side → difference.
* **Type mismatch** (file vs dir) → difference.
* **Quick**: equal size & mtime within tolerance → equal (no hash).
* **Hash**: equal size → compute hashes (selected algo) and compare.
* **Symlinks**: skipped unless `--follow-symlinks`; compare targets if followed.
* **Permissions/ACLs**: out of scope v1 (note for future).
* **Case sensitivity**: default OS behavior; override with `--case-sensitive`.

---

## 4) Performance & Scaling

* **Threads**: default `Environment.ProcessorCount`. Override `-t`.
* **Channels**: bounded; pipeline stages: enumerate → queue → N hash workers → aggregate.
* **Short-circuit**: size mismatch avoids hashing.
* **Large files**: read in 128–256 KiB blocks; async IO.
* **Cancellation**: `--timeout` or Ctrl+C → graceful drain (in interactive mode, Ctrl+C or `Q` key).

---

## 5) Logging & Verbosity

* Levels: `trace|debug|info|warn|error`.
* **Non-interactive**:

  * `info`: progress + final table + key differences
  * `warn`: suppress routine info; show anomalies (skipped/permission)
  * `error`: only errors + summary
  * `debug/trace`: per-file decisions (heavy; for diagnosing)
* **Interactive**: Log panel with filter; same levels. Cycle verbosity with `L` key.

---

## 6) Output & Artifacts

* **Console**: Spectre tables/panels/markup.
* **Files**:

  * `--json`: full per-file results (status, reason, sizes, hashes if computed) - non-interactive only.
  * `--summary`: totals + config (good for dashboards) - non-interactive only.
  * Interactive `E` export: `json|csv|md` for the current filtered view.
* **Determinism**: sorted by relative path in exports.

---

## 7) Ignore & Include

* `-i/--ignore` supports globs; repeatable.
* Optional `--include <glob>` (future).
* Reads `.fsequalignore` in either root unless `--no-ignore-file`.

---

## 8) Config & Profiles

* Config file (YAML or JSON), default search:

  * Local: `<left>/.fsequal.yml`, `<right>/.fsequal.yml`
  * Global: `~/.config/fsequal/config.yml`
* **Profiles**:

  ```yaml
  profiles:
    ci:
      algo: crc32
      threads: 8
      mode: hash
      ignore: ["**/bin/**","**/obj/**",".git/**"]
      verbosity: warn
  ```
* Use: `--profile ci` (CLI or TUI).

---

## 9) Algorithms (pluggable)

* Built-ins: `crc32`, `md5`, `sha256`.
* Optional: `xxh64` via plugin package `FsEqual.Hash.Xxh64`.
* CLI discovery: `fsequal algos` → list available.
* Per run: `-a sha256`. Per TUI: `A` cycles/chooses for selection.

---

## 10) CI/CD Recipes

**GitHub Actions (summary + artifact)**

```yaml
- name: Compare dist folders
  run: fsequal compare ./dist-prev ./dist-new -t 8 -a crc32 -m hash \
       -i "**/*.map" --json out/report.json --summary out/summary.json \
       --fail-on diff --no-progress -v warn

- name: Job Summary
  run: |
    echo "### fsEqual Summary" >> $GITHUB_STEP_SUMMARY
    jq -r '"Equal: \(.equal), Diff: \(.differences), Errors: \(.errors)"' out/summary.json >> $GITHUB_STEP_SUMMARY

- uses: actions/upload-artifact@v4
  with:
    name: fsequal-report
    path: out/
```

**Pre-release verification**

```
fsequal compare ./release-A ./release-B -i "**/docs/**" -v info --fail-on any
```

---

## 11) UX Details (Spectre bits)

**Non-interactive visuals**

* Progress: `Progress().AutoClear(false).Columns(new TaskDescriptionColumn(), new ProgressBarColumn(), new PercentageColumn(), new ElapsedTimeColumn(), new RemainingTimeColumn())`
* Tables: `Table` for differences (truncate to N rows, print hint to `--json` for full list).
* Panels: Summary panel with stats & emoji status (why not a little sparkle).

**Interactive visuals**

* Tree view: glyphs for status; color-coded nodes.
* Hotkeys overlay (`?`) uses Spectre’s key capture.
* Live refresh on pipeline events; batched UI updates to avoid flicker.

---

## 12) Security & Safety

* Open files with `FileShare.Read`.
* Respect `.gitignore` optionally (future flag).
* Warn on unreadable paths; continue (unless `--fail-on any`).

---

## 13) Roadmap (phased releases)

### V1.0 - Core (MVP)
* CLI + interactive mode (--interactive)
* Hash algos: CRC32, MD5, SHA-256
* Multi-threaded pipeline (channels)
* Ignore patterns (glob)
* JSON/summary export
* Profiles & config files
* Rich console output (Spectre.Console)
* .NET global tool distribution
* Basic tests (unit + integration)
* CI/CD (GitHub Actions)
* User docs (README, examples)

### V1.1 - Polish
* XXH64 hasher plugin
* Include globs (whitelist)
* `.gitignore` opt-in respect
* Native binaries (win/linux/macos)
* Acceptance tests (CliWrap)
* Performance benchmarks (BenchmarkDotNet)
* Code coverage >80%
* Improved error messages
* Shell completions (bash/zsh/fish)

### V1.2 - Advanced
* Self-update command
* Permissions/ACL compare (opt-in)
* Diff tool integration (launch meld/Beyond Compare)
* HTML export with interactive table
* Telemetry (opt-in, privacy-respecting)
* Plugin system (custom hashers/exporters)
* Docker image
* Package manager distribution (Homebrew, Chocolatey)

### V2.0 - Next Gen
* Snapshot/manifest mode (baseline compare)
* Watch mode (continuous monitoring)
* Remote compare (SSH, gRPC)
* VS Code extension
* GitHub Action reusable workflow
* 3-way compare (A vs B vs C)
* Change reports over time
* Advanced filters (regex, size ranges, date ranges)
* Parallel multi-folder compare (A vs B, C vs D simultaneously)

---

## 14) Complete Project Structure (with all the bells & whistles)

```
fsEqual/
  .github/
    workflows/
      ci.yml                     # Build, test, lint on every push
      release.yml                # Tag-triggered: pack tool, build natives, create GitHub release
      codeql.yml                 # Security scanning
      dependabot.yml             # Dependency updates
  src/
    FsEqual.Cli/                 # Spectre.Console.Cli commands + entry point
      Commands/
        CompareCommand.cs        # CLI pipeline runner + outputs (handles --interactive)
        AlgosCommand.cs          # List available hash algorithms
        VersionCommand.cs        # Show version, runtime info, build metadata
        BenchmarkCommand.cs      # Built-in perf benchmarks (optional)
      Options/
        CompareSettings.cs
        GlobalSettings.cs
      Ui/
        CliRenderers.cs          # Non-interactive output
        InteractiveScreens.cs    # Interactive mode screens
        Themes.cs                # Color schemes for output
      Program.cs                 # Entry point, command registration
      FsEqual.Cli.csproj         # PackAsTool=true for dotnet tool
    FsEqual.Core/                # Pipeline + domain (no UI deps)
      Pipeline/
        EnumerationStage.cs
        HashingStage.cs
        AggregationStage.cs
        PipelineCoordinator.cs
      Hashing/
        IContentHasher.cs
        Crc32Hasher.cs
        Md5Hasher.cs
        Sha256Hasher.cs
        HasherFactory.cs
      Model/
        ComparisonResult.cs
        DifferenceType.cs
        FileEntry.cs
        ComparisonStats.cs
      Config/
        ProfileLoader.cs
        IgnorePatterns.cs
      Logging/
        IComparisonLogger.cs
        StructuredLogger.cs
      Utilities/
        PathNormalizer.cs
        GlobMatcher.cs
      FsEqual.Core.csproj
    FsEqual.Hash.Xxh64/          # Optional plugin package
      Xxh64Hasher.cs
      FsEqual.Hash.Xxh64.csproj
  tests/
    FsEqual.Tests/               # Unit & integration tests
      Pipeline/
        EnumerationTests.cs
        HashingTests.cs
        PipelineIntegrationTests.cs
      Hashing/
        Crc32Tests.cs
        Md5Tests.cs
        Sha256Tests.cs
      Model/
        ResultTests.cs
      Config/
        ProfileLoaderTests.cs
        IgnorePatternTests.cs
      Commands/
        CompareCommandTests.cs
        AlgosCommandTests.cs
      Fixtures/
        TestFileSystemBuilder.cs # Helper to create test folder structures
      FsEqual.Tests.csproj
    FsEqual.BenchmarkTests/      # Performance benchmarks (BenchmarkDotNet)
      HashingBenchmarks.cs
      PipelineBenchmarks.cs
      LargeFileBenchmarks.cs
      FsEqual.BenchmarkTests.csproj
    FsEqual.AcceptanceTests/     # End-to-end CLI tests (CliWrap)
      CliOutputTests.cs
      ExitCodeTests.cs
      JsonExportTests.cs
      InteractiveModeTests.cs    # Automated TUI testing (if feasible)
      FsEqual.AcceptanceTests.csproj
  docs/
    spec.md                      # This file!
    README.md                    # User-facing guide
    CONTRIBUTING.md              # Dev setup, PR guidelines
    ARCHITECTURE.md              # System design, pipeline explanation
    CHANGELOG.md                 # Version history
    examples/
      basic-usage.md
      ci-integration.md
      profiles.md
      ignore-patterns.md
  samples/
    sample-ignore-file           # Example .fsequalignore
    sample-config.yml            # Example config with profiles
  benchmarks/                    # Test data for benchmarks
    1k-files/
    10k-files/
    large-binaries/
  scripts/
    build-natives.ps1            # Cross-compile single-file binaries (win/linux/osx)
    run-tests.ps1                # Test runner (unit + acceptance)
    pack-tool.ps1                # Create NuGet tool package
    publish-release.ps1          # Full release workflow
  .editorconfig                  # Code style
  .gitignore
  Directory.Build.props          # Common MSBuild properties (versioning, etc.)
  Directory.Packages.props       # Central package management (CPM)
  FsEqual.sln
  LICENSE                        # MIT or Apache 2.0
  README.md                      # Repo overview, quick start
  nuget.config                   # NuGet sources
  global.json                    # Pin .NET SDK version
```

---

## 15) Testing Strategy (comprehensive)

### Unit Tests (xUnit + FluentAssertions)

* **Core logic**: Pipeline stages, hashers, result aggregation.
* **Config**: Profile loading, ignore patterns, glob matching.
* **Models**: Serialization, equality, validation.
* **Mocking**: Use `TestFileSystemBuilder` fixture to create in-memory or temp folder structures.
* **Coverage target**: 80%+ for Core, 60%+ for CLI commands.

### Integration Tests

* **Pipeline end-to-end**: Feed real folders, verify results.
* **Multi-threading**: Validate thread safety, no data races.
* **Cancellation**: Ensure graceful shutdown on timeout/Ctrl+C.
* **Large files**: Test with 1GB+ files, verify streaming/chunking.

### Acceptance Tests (CliWrap)

* **CLI invocation**: Test all flags, exit codes, output formats.
* **JSON output**: Parse and validate schema.
* **Error scenarios**: Missing folders, permission denied, invalid args.
* **Interactive mode**: (Challenging) Use terminal automation or skip in CI.

### Benchmark Tests (BenchmarkDotNet)

* **Hashing speed**: Compare CRC32 vs MD5 vs SHA-256 vs XXH64.
* **Pipeline throughput**: Measure files/sec with varying thread counts.
* **Scalability**: 1k files, 10k files, 100k files.
* **Publish results**: Store in `docs/benchmarks/` for historical tracking.

### Property-Based Tests (FsCheck - optional)

* **Glob matching**: Random patterns vs paths.
* **Hash consistency**: Same file → same hash (idempotency).

### Test Data

* **Fixtures**: Pre-built folder structures (identical, diff sizes, diff content, missing files).
* **Generators**: Programmatically create N files with controlled differences.
* **Binary files**: Images, videos (test large files).
* **Edge cases**: Empty files, empty folders, symlinks, Unicode names, long paths.

---

## 16) Packaging & Distribution

### A) .NET Global Tool (primary)

* **Project**: `FsEqual.Cli.csproj`
* **Properties**:
  ```xml
  <PackAsTool>true</PackAsTool>
  <ToolCommandName>fsequal</ToolCommandName>
  <PackageId>FsEqual.Tool</PackageId>
  <Version>1.0.0</Version>
  <Authors>Your Name</Authors>
  <Description>Fast, multi-threaded folder comparison with interactive TUI</Description>
  <PackageTags>comparison;hash;folders;cli;tui;diff</PackageTags>
  <PackageProjectUrl>https://github.com/yourorg/fsequal</PackageProjectUrl>
  <RepositoryUrl>https://github.com/yourorg/fsequal</RepositoryUrl>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageIcon>icon.png</PackageIcon>
  ```
* **Build**: `dotnet pack -c Release`
* **Publish**: `dotnet nuget push bin/Release/FsEqual.Tool.1.0.0.nupkg --source https://api.nuget.org/v3/index.json`
* **Install**: `dotnet tool install -g FsEqual.Tool`
* **Update**: `dotnet tool update -g FsEqual.Tool`

### B) Native Binaries (single-file, self-contained)

* **Platforms**: win-x64, linux-x64, osx-x64, osx-arm64
* **Publish command** (per RID):
  ```powershell
  dotnet publish src/FsEqual.Cli -c Release -r win-x64 `
    -p:PublishSingleFile=true `
    -p:PublishTrimmed=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:EnableCompressionInSingleFile=true `
    --self-contained
  ```
* **Script**: `scripts/build-natives.ps1` automates for all RIDs.
* **Output**: `fsequal-1.0.0-win-x64.zip`, `fsequal-1.0.0-linux-x64.tar.gz`, etc.
* **Distribution**: Attach to GitHub Releases.

### C) Package Managers (future)

* **Homebrew**: Formula for macOS (`brew install fsequal`).
* **Chocolatey**: Package for Windows (`choco install fsequal`).
* **apt/yum**: Debian/RPM packages for Linux.
* **Scoop**: Windows package manager alternative.

### D) Container Image (optional)

* **Dockerfile**:
  ```dockerfile
  FROM mcr.microsoft.com/dotnet/runtime:8.0
  COPY --from=build /app/out /app
  ENTRYPOINT ["dotnet", "/app/FsEqual.Cli.dll"]
  ```
* **Usage**: `docker run --rm -v /hostA:/A -v /hostB:/B fsequal:latest compare /A /B`

---

## 17) CI/CD Pipeline (GitHub Actions)

### A) Continuous Integration (`ci.yml`)

Trigger: push, pull_request

```yaml
name: CI
on: [push, pull_request]
jobs:
  build-and-test:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'
      - run: dotnet restore
      - run: dotnet build -c Release --no-restore
      - run: dotnet test -c Release --no-build --verbosity normal --collect:"XPlat Code Coverage"
      - uses: codecov/codecov-action@v4
        with:
          files: '**/coverage.cobertura.xml'
  
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet format --verify-no-changes
  
  acceptance:
    runs-on: ${{ matrix.os }}
    strategy:
      matrix:
        os: [ubuntu-latest, windows-latest, macos-latest]
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
      - run: dotnet test tests/FsEqual.AcceptanceTests -c Release
```

### B) Release Pipeline (`release.yml`)

Trigger: tag push (`v*`)

```yaml
name: Release
on:
  push:
    tags: ['v*']
jobs:
  pack-tool:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - run: dotnet pack src/FsEqual.Cli -c Release -p:Version=${{ github.ref_name }}
      - run: dotnet nuget push **/*.nupkg --source https://api.nuget.org/v3/index.json --api-key ${{ secrets.NUGET_API_KEY }}
  
  build-natives:
    runs-on: ubuntu-latest
    strategy:
      matrix:
        rid: [win-x64, linux-x64, osx-x64, osx-arm64]
    steps:
      - uses: actions/checkout@v4
      - run: |
          dotnet publish src/FsEqual.Cli -c Release -r ${{ matrix.rid }} \
            -p:PublishSingleFile=true -p:PublishTrimmed=true --self-contained
          cd src/FsEqual.Cli/bin/Release/net8.0/${{ matrix.rid }}/publish
          tar -czf fsequal-${{ github.ref_name }}-${{ matrix.rid }}.tar.gz *
      - uses: actions/upload-artifact@v4
        with:
          name: fsequal-${{ matrix.rid }}
          path: '**/*.tar.gz'
  
  create-release:
    needs: [pack-tool, build-natives]
    runs-on: ubuntu-latest
    steps:
      - uses: actions/download-artifact@v4
      - uses: softprops/action-gh-release@v1
        with:
          files: '**/*.tar.gz'
          body: |
            ## Install
            **NuGet tool**: `dotnet tool install -g FsEqual.Tool --version ${{ github.ref_name }}`
            **Binaries**: Download for your platform below.
            
            See [CHANGELOG.md](https://github.com/${{ github.repository }}/blob/main/docs/CHANGELOG.md) for details.
```

### C) Security Scanning (`codeql.yml`)

```yaml
name: CodeQL
on: [push, pull_request]
jobs:
  analyze:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: github/codeql-action/init@v3
        with:
          languages: csharp
      - run: dotnet build -c Release
      - uses: github/codeql-action/analyze@v3
```

### D) Dependabot (`dependabot.yml`)

```yaml
version: 2
updates:
  - package-ecosystem: nuget
    directory: "/"
    schedule:
      interval: weekly
  - package-ecosystem: github-actions
    directory: "/.github/workflows"
    schedule:
      interval: weekly
```

---

## 18) Documentation (comprehensive)

### A) User Docs

* **README.md** (repo root):
  * Badges (build status, NuGet version, license, coverage)
  * Quick install & example
  * Link to full docs
* **docs/README.md** (user guide):
  * Installation (all methods)
  * Basic usage
  * All flags explained (with examples)
  * Interactive mode tutorial (with screenshots/GIFs)
  * Config & profiles
  * Ignore patterns
  * CI/CD recipes
  * Troubleshooting FAQ
* **docs/examples/**:
  * `basic-usage.md`: Walkthrough of common scenarios
  * `ci-integration.md`: GitHub Actions, Azure Pipelines, GitLab CI examples
  * `profiles.md`: Sample profiles for different use cases
  * `ignore-patterns.md`: Glob pattern examples
* **CHANGELOG.md**: Semantic versioning, what's new per release.

### B) Developer Docs

* **CONTRIBUTING.md**:
  * Dev environment setup (SDK, IDE)
  * Build & test instructions
  * Code style (enforced by .editorconfig + dotnet format)
  * PR checklist (tests, docs, changelog)
  * Commit message conventions (Conventional Commits)
* **ARCHITECTURE.md**:
  * System overview diagram (pipeline stages)
  * Channel-based concurrency model
  * Extensibility points (custom hashers, output formatters)
  * Performance considerations
* **API docs** (XML comments + DocFX or similar):
  * Generate HTML docs from `///` comments
  * Publish to GitHub Pages

### C) In-App Help

* **Rich help** (Spectre.Console.Cli):
  * `fsequal --help`: Command tree, descriptions
  * `fsequal compare --help`: Detailed flag explanations, examples
  * Colored, formatted, easy to scan
* **Interactive help** (`?` key in interactive mode):
  * Keybind reference overlay
  * Context-sensitive tips

---

## 19) Versioning & Release Process

### Semantic Versioning (SemVer)

* **MAJOR**: Breaking CLI changes (flag renames, exit code changes)
* **MINOR**: New features (new algos, new flags, interactive enhancements)
* **PATCH**: Bug fixes, performance improvements

### Release Checklist

1. Update `CHANGELOG.md` (under "Unreleased" → version section)
2. Bump version in `Directory.Build.props`
3. Commit: `chore: release v1.2.3`
4. Tag: `git tag v1.2.3`
5. Push: `git push origin main --tags`
6. CI/CD auto-publishes to NuGet + GitHub Releases
7. Verify installation: `dotnet tool install -g FsEqual.Tool --version 1.2.3`
8. Announce: Twitter, Reddit, GitHub Discussions, blog post

### Pre-release Channels

* **Alpha**: `1.2.3-alpha.1` (NuGet pre-release flag)
* **Beta**: `1.2.3-beta.1`
* **RC**: `1.2.3-rc.1`
* Install: `dotnet tool install -g FsEqual.Tool --version 1.2.3-beta.1`

---

## 20) Advanced Features (bells & whistles)

### A) Self-Update

* Command: `fsequal update` (checks NuGet, installs latest)
* Notify on outdated: Check version on startup (opt-out via config)

### B) Telemetry (opt-in, privacy-respecting)

* Anonymous usage stats: OS, algo used, file counts (no paths)
* Helps prioritize features
* `--telemetry-off` or config flag to disable

### C) Plugin System

* **Custom hashers**: Drop DLL in `~/.fsequal/plugins/`
* **Custom exporters**: JSON → SQLite, Elasticsearch, etc.
* Auto-discover via reflection

### D) Watch Mode

* Command: `fsequal watch <left> <right>`
* Re-compare on file system changes (FileSystemWatcher)
* Live refresh in interactive mode

### E) Diff Viewer Integration

* **3-way diff**: `fsequal compare A B --diff-tool meld`
* Launch external tool (meld, Beyond Compare, VS Code) for selected file pair
* Interactive mode: press `D` on file to diff

### F) Snapshot & Manifest Mode

* **Save baseline**: `fsequal snapshot ./A --output baseline.json`
* **Compare to baseline**: `fsequal compare ./A --baseline baseline.json`
* Use case: Detect drift over time, verify deployments

### G) Remote Compare (future)

* **SSH**: `fsequal compare ./local ssh://user@host:/remote`
* Stream hashes over network, compare locally
* Avoid copying entire folders

### H) Parallel Exports

* Export diffs to multiple formats simultaneously:
  `fsequal compare A B --json out.json --csv out.csv --html out.html`

### I) Custom Themes

* Config: `theme: dark|light|custom`
* Custom: Override Spectre color palette in config

### J) Shell Completions

* Generate for bash/zsh/fish/PowerShell:
  `fsequal completion bash > /etc/bash_completion.d/fsequal`

---

## 21) Community & Ecosystem

### A) GitHub Features

* **Issues**: Bug reports, feature requests (templates for each)
* **Discussions**: Q&A, show & tell, ideas
* **Projects**: Roadmap board (Kanban)
* **Wiki**: Community recipes, integrations

### B) Integrations

* **VS Code Extension**: Compare workspace folders, visualize diffs in sidebar
* **Azure DevOps Task**: Pipeline task for artifact comparison
* **GitHub Action**: Reusable action (`uses: yourorg/fsequal-action@v1`)

### C) Blog & Content

* Launch post: architecture, design decisions
* Performance deep-dive: benchmark results, optimization techniques
* Use case spotlights: CI, forensics, compliance

### D) Swag

* Logo, stickers, T-shirts (if it takes off!)

---

## 22) Quality Metrics & Monitoring

### A) Code Quality

* **Static analysis**: Roslyn analyzers, SonarCloud
* **Linting**: dotnet format, StyleCop
* **Complexity**: Track cyclomatic complexity, aim for simplicity

### B) Performance Tracking

* **Benchmark history**: Store BenchmarkDotNet results per release in repo
* **Regression detection**: Alert if perf degrades >10% between versions

### C) Test Metrics

* **Coverage**: Track over time (Codecov, Coveralls)
* **Test duration**: Optimize slow tests, parallelize suites

### D) User Feedback

* **In-app feedback**: `fsequal feedback` → opens GitHub issue with env info pre-filled
* **Survey**: Periodic user survey (Google Forms, TypeForm)

---

## 23) Quickstart Cheatsheet

* Compare two folders with 8 threads:

  ```
  fsequal compare A B -t 8
  ```
* Ignore build outputs and only warn:

  ```
  fsequal compare A B -i "**/bin/**" -i "**/obj/**" -v warn
  ```
* Quick mode with mtime tolerance:

  ```
  fsequal compare A B -m quick --mtime-tolerance 2
  ```
* Interactive mode:

  ```
  fsequal compare A B --interactive
  ```
* Export machine-readable report:

  ```
  fsequal compare A B --json out/report.json --summary out/summary.json --no-progress
  ```

