---
title: Installation
aliases:
  - System Requirements
  - Installing
tags:
  - deployment
  - installation
---

# Installation

## System Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| OS | Windows 10 19041 | Windows 11 22000+ |
| Architecture | x64 | x64 |
| RAM | 4 GB | 8 GB |
| Disk Space | 500 MB | 1 GB |
| .NET Framework | 4.7.2 | 4.8 |

## Installation Methods

### Method 1: GUI (Recommended)

1. Download the latest release
2. Extract to a folder
3. Run `KTWirzade.GUI.exe`
4. Drag and drop `.apbx` playbook file

### Method 2: CLI

1. Download the CLI build
2. Extract to a folder
3. Open CMD as Administrator
4. Run:
```cmd
KTWirzade.CLI.exe "path\to\playbook"
```

### Method 3: Portable

1. Extract anywhere
2. Run directly (no installation required)

## Directory Structure

### Application Directory

```
C:\Path\To\KTWirzade\
├── KTWirzade.CLI.exe
├── KTWirzade.GUI.exe
├── KTWirzade.Shared.dll
├── 7za.exe
└── Resources\
```

### Data Directory

```
%PROGRAMDATA%\KTWirzade\
├── Playbooks\
│   ├── {GUID}.apbx
│   └── {GUID}.status
├── AppliedPlaybooks\
│   └── {index}\
│       ├── playbook.conf
│       ├── playbook.png
│       └── errors.txt
├── OOBE\
│   ├── Playbook\
│   ├── OOBE.exe
│   └── oobe.conf
└── DriverCache\
```

### Windows Registry

```
HKLM\SOFTWARE\KTWirzade\Playbooks\Applied\{GUID}
├── Name
├── Username
├── Version
├── AppliedTimeUTC
├── SelectedOptions
├── ErrorLevel
└── AvailableOptions
```

## First Run

On first execution:

1. Resources are extracted from embedded archives
2. `ame-assassin` tools are set up
3. Playbook directory is created in `%PROGRAMDATA%`

## Permissions

- **Administrator**: Required for CLI and GUI
- **TrustedInstaller**: Automatically escalated for system modifications

## Uninstall

1. Delete the application directory
2. Optionally delete `%PROGRAMDATA%\KTWirzade\`
3. Optionally delete registry keys at `HKLM\SOFTWARE\KTWirzade\`

---

> [!info] See Also
> - [[Deployment/Build]] - Build instructions
> - [[CLI/Usage]] - CLI usage
