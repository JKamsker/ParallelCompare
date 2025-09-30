# `fsequal completion`

The `completion` command prints shell-completion scripts so that you can enable tab-completion for the CLI.

## Usage

```bash
fsequal completion <shell>
```

- `<shell>` – Target shell (`bash`, `zsh`, or `pwsh`). Defaults to `bash` if omitted.

## Installing completions

### Bash

```bash
fsequal completion bash > /usr/local/etc/bash_completion.d/fsequal
source /usr/local/etc/bash_completion.d/fsequal
```

### Zsh

```bash
fsequal completion zsh > ~/.zfunc/_fsequal
mkdir -p ~/.zfunc
echo 'fpath=(~/.zfunc $fpath)' >> ~/.zshrc
```

### PowerShell

```powershell
fsequal completion pwsh | Out-File -FilePath $PROFILE -Encoding utf8 -Append
```

Once the script is placed in the appropriate location, restart your shell or reload its configuration. Tab completion will then suggest commands like `compare`, `watch`, `snapshot`, and `completion`, as well as top-level options such as `--help` or `--version`.
