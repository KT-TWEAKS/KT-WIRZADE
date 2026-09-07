# Safe regression checks

Build Shared + CLI in Release/x64 first, then run on Windows with .NET Framework 4.8:

```powershell
dotnet build tests/Regression/Regression.csproj -c Release
& ./tests/Regression/bin/Release/net48/Regression.exe
```

The executable returns a nonzero exit code on failure. It checks the GitHub JSON contract, version comparisons, both spellings of registry and PowerShell YAML tags (including colon forms), and invalid download arguments. It does not execute parsed actions or apply playbooks. The download checks must fail validation before any filesystem or network access.

The suite references the freshly built Shared library rather than the embedded GUI copy. GitHub Actions runs it after building Shared + CLI.
