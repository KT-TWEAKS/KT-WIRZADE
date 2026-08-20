---
title: CLI Usage
aliases:
  - Usage Examples
  - Commands
tags:
  - cli
  - usage
---

# CLI Usage

## Basic Commands

### Execute a Playbook

```cmd
KTWirzade.CLI.exe "C:\Playbooks\my-playbook"
```

The CLI expects the first argument to be a **directory** containing the playbook files.

### Execute with Options

```cmd
KTWirzade.CLI.exe "C:\Playbooks\my-playbook" privacy optimize
```

Additional arguments after the path are treated as selected options.

## Execution Examples

### Minimal Execution

```cmd
:: Simplest form - uses default options
KTWirzade.CLI.exe "C:\MyPlaybook"
```

### With Specific Options

```cmd
:: Select specific feature options
KTWirzade.CLI.exe "C:\MyPlaybook" option1 option2
```

### Single File Build

```cmd
:: Using single-file build
KTWirzade.CLI.exe "C:\MyPlaybook" privacy
```

## Interprocess Mode

Used internally by the system for privilege escalation:

```cmd
KTWirzade.CLI.exe "directory" Interprocess Administrator --Mode TwoWay --Nodes Level=User:ProcessID=1234
```

### Parameters

| Parameter | Description |
|-----------|-------------|
| `Administrator` | Target level |
| `--Mode TwoWay` | Bidirectional communication |
| `--Nodes` | Connected node list |
| `--Host` | Host process ID |

## Output Format

### Progress Updates

```
50% Disabling services...
75% Modifying registry...
90% Removing bloatware...
100% Deploying the selected Playbook configuration...
```

### Status Messages

```
Starting Playbook...
Checking requirements...
Extracting resources...
Preparing system...
Playbook completed successfully.
```

### Error Messages

```
This program must be launched as an Administrator!
No Playbook selected.
Configuration folder is empty...
Playbook completed with errors.
```

## Exit Codes

| Code | Meaning |
|------|---------|
| `0` | Success |
| `-1` | Error (admin required, invalid path, etc.) |
| `1` | Initialization or extraction error |
| `376` | Interprocess connection established |

## Common Scenarios

### Fresh Installation

```cmd
:: Run on clean Windows install
KTWirzade.CLI.exe "C:\Playbooks\privacy-setup"
```

### With Defender Disabled

```cmd
:: Playbook requires Defender to be off first
KTWirzade.CLI.exe "C:\Playbooks\debloat"
```

### Automated Deployment

```cmd
:: Silent execution for deployment
KTWirzade.CLI.exe "C:\Playbooks\enterprise-config" silent
```

## Troubleshooting

### "Must be run as Administrator"

Run CMD as Administrator before executing.

### "No Playbook selected"

Ensure the path points to a **directory**, not a `.apbx` file.

### "Configuration folder is empty"

Extract the `.apbx` first:
```cmd
7z x playbook.apbx -p"malte" -o.\extracted\
KTWirzade.CLI.exe .\extracted\
```

---

> [!info] See Also
> - [[CLI/Overview]] - Entry point details
> - [[Architecture/Execution-Flow]] - Execution pipeline
