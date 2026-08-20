---
title: Contributing
aliases:
  - Development Setup
  - How to Contribute
tags:
  - development
  - contributing
---

# Contributing

## Development Setup

### Prerequisites

- Windows 10/11 (x64)
- Visual Studio Build Tools 2022 (or full VS 2022)
- .NET SDK 8.0+ (for GUI project)
- .NET Framework 4.7.2 and 4.8 Targeting Packs
- Git

### Getting Started

```bash
git clone https://github.com/your-repo/KT-Wirzade.git
cd KT-Wirzade
```

### Build System

### Automated Build (Recommended)

```cmd
build.bat
```

Executes in sequence:
1. NuGet restore
2. Build Shared + CLI via MSBuild (legacy csproj)
3. Copy `KTWirzade.Shared.dll` + dependencies (YamlDotNet, TimeZoneConverter, JetBrains.Annotations) to GUI Resources
4. Build GUI via dotnet CLI (SDK-style)
5. Copy Shared dependencies + native DLLs to GUI output directory

### Manual Build

The project uses **two different build systems**:

| Project | Type | Tool |
|---------|------|------|
| `KTWirzade.Shared` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.CLI` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.GUI` | SDK-style csproj (.NET 4.8) | dotnet CLI |

```cmd
:: Step 1: Build Shared + CLI
msbuild KT-Wirzade.sln /t:Build /p:Configuration=Release /p:Platform=x64 /p:SolutionDir="<path>\\" /m

:: Step 2: Copy Shared.dll + dependencies to GUI Resources
copy /Y KTWirzade.Shared\bin\x64\Release\KTWirzade.Shared.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\JetBrains.Annotations.dll KTWirzade.GUI\src\Resources\

:: Step 3: Build GUI
dotnet build KTWirzade.GUI\src\KTWirzade.GUI.csproj -c Release -p:Platform=x64 -p:SolutionDir="<path>\\"

:: Step 4: Copy Shared deps to GUI output dir
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\7z.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\client-helper.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\KTWirzade.GUI.exe.config CLI-Standalone\
```

> [!warning] The GUI project is **not** in `KT-Wirzade.sln` because MSBuild from BuildTools cannot resolve the `Microsoft.NET.Sdk`. Build it separately with `dotnet`.

> [!important] The GUI **requires** Shared dependencies (YamlDotNet.dll, TimeZoneConverter.dll, JetBrains.Annotations.dll, 7z.dll, client-helper.dll) in the output directory. Without them, it crashes on startup.

> [!warning] **ALINHAMENTO DE PACOTES OBRIGATORIO**: Os 3 projetos (Shared, CLI, GUI) devem referenciar **as mesmas versoes** dos pacotes NuGet. Caso contrario, o GUI fecha imediatamente com `FileLoadException`. Pacotes criticos:
> - `System.Text.Json` (10.0.7)
> - `TaskScheduler` / `Microsoft.Win32.TaskScheduler` (2.12.2)
> - `Polly.Core` (8.6.6)
> - `SharpSevenZip` (2.0.36)
> - `Newtonsoft.Json` (13.0.4)
> - `JetBrains.Annotations` (2024.2.0-eap1)
> 
> Ao copiar o GUI para outra pasta (ex: CLI-Standalone), **copie tambem** o `KTWirzade.GUI.exe.config` (binding redirects) e todas DLLs do diretorio original. Sem o .config, dependencias como `System.Threading.Tasks.Extensions` falham ao carregar.

### Build Output

```
KTWirzade.CLI\bin\x64\Release\KTWirzade.CLI.exe          (~4.2 MB)
KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\KTWirzade.GUI.exe  (~24.8 MB)
```

## Project Structure

```
├── KTWirzade.CLI/          # CLI entry point
├── KTWirzade.Shared/       # Core engine
├── KTWirzade.GUI/          # WPF interface
├── Core/                   # Low-level infrastructure
├── Interprocess/           # IPC framework
└── ManagedWimLib/          # WIM manipulation
```

## Development Workflow

1. Create a feature branch
2. Make changes
3. Test on a clean Windows installation
4. Submit a pull request

## Testing

### Manual Testing

- Test on clean Windows VM
- Test with different Windows builds
- Test ISO mode if applicable
- Test upgrade scenarios

### Test Checklist

- [ ] Playbook loads without errors
- [ ] All actions execute correctly
- [ ] Requirements are properly checked
- [ ] Progress reporting works
- [ ] Errors are handled gracefully
- [ ] GUI displays correctly
- [ ] Themes work (Dark/Light)

## Code Guidelines

- Follow existing patterns
- Use descriptive variable names
- Add XML documentation for public APIs
- Keep methods focused and small

---

> [!info] See Also
> - [[Development/Code-Style]] - Code style guide
> - [[Development/TODO]] - Planned improvements
