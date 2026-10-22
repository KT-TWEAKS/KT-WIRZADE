param([string]$Version = '1.0.2')
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Push-Location $projectRoot
try {
    $versionSource = Get-Content KTWirzade.Shared/Globals.cs -Raw
    if ($versionSource -notmatch ('CurrentVersion = "' + [regex]::Escape($Version) + '"')) { throw 'Version mismatch in Globals.cs' }
    $msbuildCommand = Get-Command msbuild -ErrorAction SilentlyContinue
    if ($msbuildCommand) { $buildTool = $msbuildCommand.Source }
    else {
        $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio/Installer/vswhere.exe'
        $buildTool = & $vswhere -latest -products '*' -requires Microsoft.Component.MSBuild -find 'MSBuild/**/Bin/amd64/MSBuild.exe' | Select-Object -First 1
    }
    if (!$buildTool -or !(Test-Path -LiteralPath $buildTool)) { throw 'MSBuild not found.' }
    $helperDir = Join-Path $projectRoot 'Core/Helper/x64/Release'
    New-Item -ItemType Directory -Force $helperDir | Out-Null
    Copy-Item KTWirzade.GUI/src/Resources/client-helper.dll $helperDir -Force
    & $buildTool KT-Wirzade.sln /restore /t:Rebuild /p:Configuration=Release /p:Platform=x64 /p:PostBuildEvent= /v:minimal /nologo
    if ($LASTEXITCODE -ne 0) { throw 'Shared/CLI build failed.' }
    $shared = Join-Path $projectRoot 'KTWirzade.Shared/bin/x64/Release'
    $resources = Join-Path $projectRoot 'KTWirzade.GUI/src/Resources'
    Get-ChildItem $shared -Filter *.dll | Copy-Item -Destination $resources -Force
    Copy-Item KTWirzade.CLI/bin/x64/Release/KTWirzade.CLI.exe $resources -Force
    Copy-Item KTWirzade.CLI/bin/x64/Release/KTWirzade.CLI.exe.config $resources -Force
    dotnet build KTWirzade.GUI/src/KTWirzade.GUI.csproj -c Release -p:Platform=x64 -p:PostBuildEvent= --nologo
    if ($LASTEXITCODE -ne 0) { throw 'GUI build failed.' }

    $gui = Join-Path $projectRoot 'KTWirzade.GUI/src/bin/x64/Release/net4.8-windows'
    $artifactRoot = Join-Path $projectRoot 'artifacts'
    New-Item -ItemType Directory -Force $artifactRoot | Out-Null
    $singleExe = Join-Path $artifactRoot "KT-WIRZADE-v$Version-win-x64.exe"
    Copy-Item -LiteralPath (Join-Path $gui 'KTWirzade.GUI.exe') -Destination $singleExe -Force

    $actual = [Diagnostics.FileVersionInfo]::GetVersionInfo($singleExe).FileVersion
    if ($actual -notin @($Version, "$Version.0")) { throw "Stale executable: $actual" }
    if ((Get-Item -LiteralPath $singleExe).Length -lt 20MB) { throw 'Single-file executable is missing embedded resources.' }

    $requiredResources = @(
        'KTWirzade.GUI.Resources.KTWirzade.CLI.exe',
        'KTWirzade.GUI.Resources.KTWirzade.Shared.dll',
        'KTWirzade.GUI.Resources.FluentIcons.Common.dll',
        'KTWirzade.GUI.Resources.FluentIcons.WPF.dll',
        'KTWirzade.GUI.Resources.7z.dll'
    )
    $resourceList = & powershell.exe -NoProfile -Command "[Reflection.Assembly]::ReflectionOnlyLoadFrom('$($singleExe.Replace("'", "''"))').GetManifestResourceNames()"
    if ($LASTEXITCODE -ne 0) { throw 'Could not inspect embedded resources.' }
    foreach ($resource in $requiredResources) {
        if ($resourceList -notcontains $resource) { throw "Missing embedded resource: $resource" }
    }

    $digest = (Get-FileHash -LiteralPath $singleExe -Algorithm SHA256).Hash.ToLowerInvariant()
    $manifestPath = Join-Path $artifactRoot "KT-WIRZADE-v$Version-manifest.json"
    $sourceCommit = (& git rev-parse HEAD).Trim()
    $workingTreeChanges = @(& git status --short)
    [ordered]@{
        version = $Version
        sourceCommit = $sourceCommit
        workingTreeDirty = ($workingTreeChanges.Count -gt 0)
        workingTreeChanges = $workingTreeChanges
        file = [ordered]@{ name=(Split-Path $singleExe -Leaf); size=(Get-Item $singleExe).Length; sha256=$digest }
        singleExecutable = $true
        embeddedApplicationDependencies = $true
        targetFramework = '.NET Framework 4.8 (precisa estar instalado no Windows)'
        runtimeEmbedded = $false
    } | ConvertTo-Json -Depth 4 | Set-Content -Encoding utf8 $manifestPath
    "$digest  $(Split-Path $singleExe -Leaf)" | Set-Content -Encoding ascii (Join-Path $artifactRoot 'SHA256SUMS.txt')
    Write-Host "Single-file release verified: $singleExe"
} finally { Pop-Location }
