---
title: TODO
aliases:
  - Planned Improvements
  - Missing Features
tags:
  - development
  - todo
---

# TODO

## Missing Features

### Action Types

- [ ] `!lineInFile` - Line-in-file operations (currently commented out)
- [ ] `!user` - User account management (currently commented out)
- [ ] `!shortcut` - Shortcut creation (currently commented out)
- [ ] `!update` - Update operations (currently commented out)

### Parser Improvements

- [ ] Support for arrays of arrays in YAML
- [ ] More granular condition evaluation
- [ ] Better error messages for malformed YAML

### GUI Enhancements

- [ ] Drag-and-drop reordering of feature pages
- [ ] Preview mode for playbook actions
- [ ] Undo/redo for configuration changes
- [ ] Custom theme creation tool

### CLI Improvements

- [ ] Interactive mode for step-by-step execution
- [ ] Dry-run mode (validate without executing)
- [ ] Verbose logging option
- [ ] JSON output mode for automation

### Verification System

- [ ] HTTPS support for API endpoints
- [ ] API key authentication
- [ ] Offline verification cache
- [ ] Batch verification

### ISO Operations

- [ ] Multi-ISO batch processing
- [ ] Custom ISO layout templates
- [ ] UEFI/Legacy dual-boot support
- [ ] ISO diff/patch system

## Planned Improvements

### Performance

- [ ] Parallel action execution where safe
- [ ] Caching for repeated operations
- [ ] Lazy loading for GUI resources

### Testing

- [ ] Unit tests for parser
- [ ] Integration tests for actions
- [ ] GUI automation tests
- [ ] CI/CD pipeline

### Documentation

- [ ] API documentation for all public methods
- [ ] Video tutorials
- [ ] Troubleshooting guide
- [ ] FAQ section

### Code Quality

- [ ] Replace `Wrap.ExecuteSafe` with Result pattern
- [ ] Modernize to async/await throughout
- [ ] Remove deprecated code
- [ ] Add nullable annotations

### Platform Support

- [ ] ARM64 native support
- [ ] Windows Server support
- [ ] Windows PE support

## Known Issues

### High Priority

- [x] **GUI crash on startup** - `7z.dll` path used `CurrentDirectory` instead of exe directory. Fixed in `App.xaml.cs`
- [x] **`FileNotFoundException` in Log static constructor** - Shared dependencies (YamlDotNet.dll, etc.) not copied to GUI output. Fixed in `build.bat` step 5
- [x] **"Playbook failed" - Could not initialize process** - `LaunchNode` parameters swapped in `ProgressDialog.xaml.cs`. Fixed to match CLI
- [x] **`ame-assassin.exe` not found** - `AppxAction` and `SystemPackageAction` used `Directory.GetCurrentDirectory()` which points to `%TEMP%` when running as TrustedInstaller. Fixed to use `AmeliorationUtil.Playbook.Path + "\\Executables"` (correct extracted playbook path)
- [x] **`TaskKillAction` did not protect CLI process** - `RegexNoKill` still used old `"TrustedUninstaller\\.CLI"` name. Fixed to `"KTWirzade\\.CLI"`
- [x] **`7z.dll` not found in Drivers** - `SharpSevenZipBase.SetLibraryPath` used `Directory.GetCurrentDirectory()`. Fixed to `AppDomain.CurrentDomain.BaseDirectory`
- [x] **Log message "Skipping TU.CLI" inconsistent** - Process name check was correct but log message still said "TU.CLI". Fixed to "Skipping KTWirzade.CLI..."
- [x] **`AppxAction` and `SystemPackageAction` crash when `ame-assassin.exe` is missing** - Added PowerShell fallback (Remove-AppxPackage / Remove-WindowsPackage)
- [x] **`ScheduledTaskAction` double dispose** - Manual Dispose() calls inside `using var` blocks. Fixed
- [x] **`SoftwareAction` HttpClient leak + double dispose** - Fixed using blocks
- [x] **`RegistryKeyAction` RegistryKey leak** (Shared + Core) - Fixed with `using` blocks
- [x] **`ServiceAction` NullReferenceException** when registry value is null - Fixed
- [x] **`FileAction` `LastIndexOf == -1` bug** - Fixed guard in both GetStatus and RunTask
- [x] **`CLI` resource extraction off-by-one** - Fixed loop
- [x] **`FinishErrorPageViewModel` self-assignment** - Fixed with `this.`
- [x] **`WinUtil` FileStream leak + null resource** - Fixed
- [x] **`AmeliorationUtil` IconPath null safety** - Fixed
- [x] **`process.Start()` return value ignored** in Defender, AmeliorationUtil, SoftwareAction, OutputProcessor - Fixed
- [x] **`SoftwareAction` 7za.exe path** - Fixed with `AppDomain.CurrentDomain.BaseDirectory`
- [x] **`RequirementsPageView` event leak** - Fixed by unsubscribing at end of method (both views)
- [ ] File deletion can hang on locked files
- [ ] Service deletion may fail for driver services
- [ ] UWP app removal inconsistent across builds

### Medium Priority

- [ ] Progress reporting may lag behind actual progress
- [ ] Theme detection fails on some custom Windows installs
- [ ] ISO mode limited to x64

### Low Priority

- [ ] Minor icon scaling issues on high DPI
- [ ] Tooltip text truncation on narrow windows
- [ ] Satellite resource cleanup not always complete

## Technical Debt

- [ ] Remove unused `IWshRuntimeLibrary` COM reference (already commented out)
- [ ] Consolidate duplicate Win32 interop code
- [ ] Standardize error handling across all actions
- [ ] Update to newer .NET version
- [ ] Resolve CS1998 warnings (async methods without await)
- [ ] Resolve CS0649 warnings (unassigned struct fields in P/Invoke)
- [ ] Migrate legacy csproj (Shared/CLI) to SDK-style

## Build System

- [x] Create automated build script (`build.bat`)
- [x] Fix shared DLL copy to GUI Resources
- [x] Copy Shared dependencies (YamlDotNet, TimeZoneConverter, JetBrains.Annotations) to GUI output
- [x] Copy native DLLs (7z.dll, client-helper.dll) to GUI output
- [x] Fix `7z.dll` path resolution in GUI (use exe directory, not CurrentDirectory)
- [x] **Alinhar pacotes NuGet entre os 3 projetos** (v0.8.5) — GUI fechava com `FileLoadException` por versoes divergentes de `System.Text.Json`, `Microsoft.Win32.TaskScheduler`, `Polly.Core`, `SharpSevenZip`, `Newtonsoft.Json`, `JetBrains.Annotations`
- [x] **Copiar `KTWirzade.GUI.exe.config` para CLI-Standalone** — sem o config, `System.Threading.Tasks.Extensions` nao carregava
- [x] **Criar `KTWirzade.GUI\src\App.config`** com todos os redirects necessarios (TaskScheduler 2.12.1.0 → 2.12.2.0, System.Net.Http 4.2.0.0 → 4.1.1.3, etc.) e configurar `<AppConfig>App.config</AppConfig>` no csproj
- [x] **Desabilitar `AutoGenerateBindingRedirects`** no csproj do GUI para que o App.config customizado nao seja sobrescrito pelo auto-generate
- [ ] Implement CI/CD pipeline (Gitea Actions)
- [ ] Add build artifact publishing

## Recent Fixes (KT WIRZADE Rename)

- [x] Renamed all namespaces from Ameliorated/AME to KTWirzade
- [x] Updated all pack URIs and resource streams
- [x] Fixed Assembly info (names, titles, descriptions)
- [x] Created `WindowsUpdate` model class (missing from Shared)
- [x] Added `ExcludedWindowsUpdates`/`ExcludeBadWindowsUpdates` to Playbook
- [x] Fixed `Defender.KillAndDisable` 4th parameter (`noSafeBoot`)
- [x] Fixed `Plane.dll` HintPath in GUI csproj
- [x] Fixed `KTWirzade.Shared.dll` HintPath in GUI csproj
- [x] Stubbed WUApiLib in Requirements.cs (placeholder)
- [x] Created licensing site (`licensing-site/`)
- [x] Created Obsidian docs vault (`docs/`)
- [x] Fixed `7z.dll` path resolution - uses exe directory instead of CurrentDirectory
- [x] Fixed Shared dependencies not copied to GUI output (YamlDotNet, TimeZoneConverter, etc.)
- [x] Fixed `LaunchNode` parameter swap in ProgressDialog.xaml.cs (TargetLevel/Level were inverted)
- [x] Fixed `ame-assassin.exe` not found - `Directory.GetCurrentDirectory()` changed to `AmeliorationUtil.Playbook.Path + "\\Executables"` in AppxAction.cs and SystemPackageAction.cs (correct path when running as TrustedInstaller)
- [x] **Added PowerShell fallback** when `ame-assassin.exe` does not exist - AppxAction uses `Remove-AppxPackage` (Family/Package/App), SystemPackageAction uses `Remove-WindowsPackage`
- [x] Fixed `7z.dll` not found in Drivers - `Directory.GetCurrentDirectory()` changed to `AppDomain.CurrentDomain.BaseDirectory` in Drivers.cs
- [x] Fixed `TaskKillAction` not protecting CLI process - `RegexNoKill` updated from `TrustedUninstaller\\.CLI` to `KTWirzade\\.CLI`
- [x] Fixed inconsistent log messages - "Skipping TU.CLI..." changed to "Skipping KTWirzade.CLI..." in FileAction.cs
- [x] **Fixed `ScheduledTaskAction` double dispose** - removed manual `Dispose()` calls inside `using var` blocks
- [x] **Fixed `SoftwareAction` HttpClient/Response leaks** - `InstallToCache` now uses `using var httpClient`, `response` wrapped in `using`
- [x] **Fixed `RegistryKeyAction` RegistryKey leaks** (Shared + Core) - added `using` blocks in `DeleteKeyTreeWin32`
- [x] **Fixed `ServiceAction` NullReferenceException** - `(int)value` replaced with null check + `Convert.ToInt32`
- [x] **Fixed `FileAction` `LastIndexOf == -1` bug** - added guard in both `GetStatus` and `RunTask`
- [x] **Fixed `CLI` resource extraction off-by-one** - replaced broken loop with clean seek/read/write
- [x] **Fixed `FinishErrorPageViewModel` self-assignment** - `finishErrorPage = finishErrorPage` now uses `this.`
- [x] **Fixed `WinUtil` FileStream leak + null resource** - `using` block + null check
- [x] **Fixed `AmeliorationUtil` IconPath null safety** - Directory.Exists check + empty string fallback
- [x] **Fixed `process.Start()` return value ignored** in Defender, AmeliorationUtil, SoftwareAction, OutputProcessor
- [x] **Fixed `SoftwareAction` 7za.exe path** - uses `AppDomain.CurrentDomain.BaseDirectory` with fallback
- [x] **Fixed `RequirementsPageView` event leak** - added `base.Loaded -= CheckRequirements` at end of method (both views)
- [x] Added `build.bat` with 5-step automated build pipeline

---

> [!info] See Also
> - [[Development/Contributing]] - Contributing guide
> - [[Development/Code-Style]] - Code style guide
