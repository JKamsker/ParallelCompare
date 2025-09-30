# `fsequal snapshot`

The `snapshot` command records the state of a directory tree so that future comparisons can reuse the manifest via `--baseline`.

## Usage

```bash
fsequal snapshot <left> --output <path>
```

- `<left>` – Directory to capture.
- `--output <path>` – Required destination path for the manifest (JSON).

All common options from [`compare`](command-compare.md) still apply. For example, you can provide ignore patterns, algorithms, or profiles to ensure the snapshot reflects the desired configuration.

## Examples

Capture a baseline that uses SHA-256 hashing and custom ignores:

```bash
fsequal snapshot ./src --output artifacts/baseline.json --algo sha256 --ignore "**/bin/**" --ignore "**/obj/**"
```

Export to a temporary directory ready for distribution:

```bash
fsequal snapshot ./src --output $(mktemp -d)/baseline.json
```
