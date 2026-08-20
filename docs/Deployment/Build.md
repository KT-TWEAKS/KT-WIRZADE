---
title: Build Process
aliases:
  - Building
  - Compilation
tags:
  - deployment
  - build
---

# Build Process

## Requirements

| Requirement | Version |
|-------------|---------|
| Visual Studio Build Tools | 2022 (with "Desktop development with C++") |
| .NET SDK | 8.0+ (for GUI project) |
| .NET Framework Targeting Pack | 4.7.2 (CLI/Shared), 4.8 (GUI) |
| NuGet CLI | Latest |
| Platform | x64 only |

## Automated Build (Recommended)

```cmd
build.bat
```

Executes in sequence:
1. **NuGet restore** for all packages
2. **Build Shared + CLI** via MSBuild (legacy csproj)
3. **Copy Shared.dll + dependencies** to GUI Resources (YamlDotNet, TimeZoneConverter, JetBrains.Annotations)
4. **Build GUI** via dotnet CLI (SDK-style)
5. **Copy dependencies** to GUI output (Shared DLLs, 7z.dll, client-helper.dll)

## Manual Build

### Build System Overview

| Project | Type | Tool |
|---------|------|------|
| `KTWirzade.Shared` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.CLI` | Legacy csproj (.NET 4.7.2) | MSBuild |
| `KTWirzade.GUI` | SDK-style csproj (.NET 4.8) | dotnet CLI |

> [!warning] The GUI project is **not** in `KT-Wirzade.sln` because MSBuild from BuildTools cannot resolve the `Microsoft.NET.Sdk`. Build it separately with `dotnet`.

### Step 1: Build Shared + CLI

```cmd
msbuild KT-Wirzade.sln /t:Build /p:Configuration=Release /p:Platform=x64 /p:SolutionDir="<path>\\" /m
```

### Step 2: Copy Shared.dll + dependencies to GUI Resources

```cmd
copy /Y KTWirzade.Shared\bin\x64\Release\KTWirzade.Shared.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\Resources\
copy /Y KTWirzade.Shared\bin\x64\Release\JetBrains.Annotations.dll KTWirzade.GUI\src\Resources\
```

### Step 3: Build GUI

```cmd
dotnet build KTWirzade.GUI\src\KTWirzade.GUI.csproj -c Release -p:Platform=x64 -p:SolutionDir="<path>\\"
```

### Step 4: Copy dependencies to GUI output

```cmd
copy /Y KTWirzade.Shared\bin\x64\Release\YamlDotNet.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\TimeZoneConverter.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\7z.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
copy /Y KTWirzade.Shared\bin\x64\Release\client-helper.dll KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
```

> [!important] Without these dependencies, the GUI crashes on startup with `FileNotFoundException`.

## Build Output

```
KTWirzade.CLI\bin\x64\Release\
  ├── KTWirzade.CLI.exe          (~4.2 MB)
  ├── KTWirzade.CLI.exe.config
  ├── KTWirzade.Shared.dll
  └── ...

KTWirzade.GUI\src\bin\x64\Release\net4.8-windows\
  ├── KTWirzade.GUI.exe          (~24.8 MB)
  ├── KTWirzade.Shared.dll
  ├── YamlDotNet.dll
  ├── TimeZoneConverter.dll
  ├── 7z.dll
  ├── client-helper.dll
  └── ...
```

## Embedded Resources

### CLI Embeds
- `7za.dll`, `7za.exe`, `7zxa.dll`
- `CLI-Resources.7z`
- `ProcessInformer.7z`
- `Z-AME-NoDefender-*.cab` (placeholder .cab files for development)

### Shared Embeds
- `UsrClass.dat`
- `uefi-ntfs-ame.img`
- `Z-AME-NoDefender-*.cab`

## Clean Build

```cmd
:: Clean
msbuild KT-Wirzade.sln /t:Clean /p:Configuration=Release /p:Platform=x64

:: Rebuild
build.bat
```

---

> [!info] See Also
> - [[Deployment/Installation]] - Installation guide
> - [[Development/Contributing]] - Development setup
