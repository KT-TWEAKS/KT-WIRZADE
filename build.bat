@echo off
setlocal enabledelayedexpansion

echo ============================================
echo    KT WIRZADE - Build Script v1.0.0
echo ============================================
echo.

set MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe
set DOTNET=C:\dotnet-sdk\dotnet.exe
set BASE=%~dp0
set CONFIG=Release
set PLATFORM=x64

if "%BASE:~-1%"=="\" set BASE=%BASE:~0,-1%

set SHARED_BIN=%BASE%\KTWirzade.Shared\bin\%PLATFORM%\%CONFIG%
set CLI_OUT=%BASE%\KTWirzade.CLI\bin\%PLATFORM%\%CONFIG%
set GUI_OUT=%BASE%\KTWirzade.GUI\src\bin\%PLATFORM%\%CONFIG%\net4.8-windows
set DEVTOOL_OUT=%BASE%\KTWirzade.DevTool\bin\%PLATFORM%\%CONFIG%\net4.8-windows
set RES=%BASE%\KTWirzade.GUI\src\Resources
set STANDALONE=%BASE%\CLI-Standalone

if not exist "%MSBUILD%" (
    echo [ERRO] MSBuild nao encontrado.
    exit /b 1
)
if not exist "%DOTNET%" (
    echo [ERRO] dotnet SDK nao encontrado.
    exit /b 1
)

echo [1/6] Preparando client-helper.dll nativo (necessario se nao houver compilador C++)...
if not exist "%BASE%\Core\Helper\x64\Release\client-helper.dll" (
    if exist "%RES%\client-helper.dll" (
        if not exist "%BASE%\Core\Helper\x64\Release" mkdir "%BASE%\Core\Helper\x64\Release"
        copy /Y "%RES%\client-helper.dll" "%BASE%\Core\Helper\x64\Release\client-helper.dll" >nul
        echo   - client-helper.dll copiado para Core\Helper\x64\Release\
    ) else (
        echo   [ERRO] client-helper.dll nao encontrado em Resources nem em Core\Helper.
        exit /b 1
    )
)

echo.
echo [2/6] Restaurando pacotes NuGet...
"%MSBUILD%" "%BASE%\KT-Wirzade.sln" /t:Restore /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /p:SolutionDir="%BASE%\\" /v:minimal
if errorlevel 1 (
    echo [ERRO] Falha ao restaurar pacotes NuGet.
    exit /b 1
)

echo.
echo [3/6] Compilando Shared + CLI via solution (GUI/DevTool/DevKit vem nos passos seguintes)...
"%MSBUILD%" "%BASE%\KT-Wirzade.sln" /t:Build /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /p:SolutionDir="%BASE%\\" /m /v:minimal
if errorlevel 1 (
    echo [ERRO] Falha ao compilar Shared + CLI.
    exit /b 1
)

echo.
echo [4/6] Copiando Shared.dll e dependencias para Resources do GUI...
set COPIAS_FALHARAM=0
copy /Y "%SHARED_BIN%\KTWirzade.Shared.dll" "%RES%\KTWirzade.Shared.dll" >nul
if errorlevel 1 set COPIAS_FALHARAM=1
copy /Y "%SHARED_BIN%\YamlDotNet.dll" "%RES%\YamlDotNet.dll" >nul
if errorlevel 1 set COPIAS_FALHARAM=1
copy /Y "%SHARED_BIN%\TimeZoneConverter.dll" "%RES%\TimeZoneConverter.dll" >nul
if errorlevel 1 set COPIAS_FALHARAM=1
copy /Y "%SHARED_BIN%\JetBrains.Annotations.dll" "%RES%\JetBrains.Annotations.dll" >nul
if errorlevel 1 set COPIAS_FALHARAM=1
for %%f in ("%SHARED_BIN%\*.dll") do copy /Y "%%f" "%RES%\" >nul 2>&1
if errorlevel 1 set COPIAS_FALHARAM=1

rem The GUI embeds Resources\KTWirzade.CLI.exe as a resource (Launched from
rem ProgressPageView). Without this step the stale committed copy (old
rem assembly identity) keeps shipping inside the GUI.
copy /Y "%CLI_OUT%\KTWirzade.CLI.exe" "%RES%\KTWirzade.CLI.exe" >nul
if errorlevel 1 set COPIAS_FALHARAM=1
if "%COPIAS_FALHARAM%"=="1" (
    echo [ERRO] Falha ao copiar Shared.dll/dependencias/CLI.exe para Resources do GUI.
    exit /b 1
)
echo   - KTWirzade.CLI.exe atualizado em Resources

echo.
echo [5/6] Compilando GUI, APBX Developer e APBX DevKit (dotnet)...
"%DOTNET%" build "%BASE%\KTWirzade.GUI\src\KTWirzade.GUI.csproj" -c %CONFIG% -p:Platform=%PLATFORM% -p:SolutionDir="%BASE%\\" -v minimal
if errorlevel 1 (
    echo [ERRO] Falha ao compilar GUI.
    exit /b 1
)

if exist "%BASE%\KTWirzade.DevTool\KTWirzade.DevTool.csproj" (
    "%DOTNET%" build "%BASE%\KTWirzade.DevTool\KTWirzade.DevTool.csproj" -c %CONFIG% -p:Platform=%PLATFORM% -v minimal
    if errorlevel 1 (
        echo [ERRO] Falha ao compilar APBX Developer.
        exit /b 1
    )
) else (
    echo   [AVISO] KTWirzade.DevTool nao encontrado no repositorio - pulando APBX Developer.
)

if exist "%BASE%\KTWirzade.DevKit\KTWirzade.DevKit.csproj" (
    "%DOTNET%" build "%BASE%\KTWirzade.DevKit\KTWirzade.DevKit.csproj" -c %CONFIG% -p:Platform=%PLATFORM% -v minimal
    if errorlevel 1 (
        echo [ERRO] Falha ao compilar APBX DevKit.
        exit /b 1
    )
) else (
    echo   [AVISO] KTWirzade.DevKit nao encontrado no repositorio - pulando APBX DevKit.
)

echo.
echo [6/6] Copiando dependencias Shared para output do GUI e CLI-Standalone...
set FALHAS=0

for %%f in ("%SHARED_BIN%\*.dll") do (
    if exist "%%f" (
        copy /Y "%%f" "%GUI_OUT%\" >nul 2>&1
        if errorlevel 1 set FALHAS=1
        copy /Y "%%f" "%CLI_OUT%\" >nul 2>&1
        if errorlevel 1 set FALHAS=1
        copy /Y "%%f" "%STANDALONE%\" >nul 2>&1
        if errorlevel 1 set FALHAS=1
    )
)

rem The standalone GUI needs its own managed dependencies next to the exe; without
rem this loop a clean machine gets a GUI that crashes with FileNotFoundException.
for %%f in ("%GUI_OUT%\*.dll") do (
    if exist "%%f" (
        copy /Y "%%f" "%STANDALONE%\" >nul 2>&1
        if errorlevel 1 set FALHAS=1
    )
)
copy /Y "%CLI_OUT%\KTWirzade.CLI.exe" "%STANDALONE%\KTWirzade.CLI.exe" >nul 2>&1
if errorlevel 1 set FALHAS=1
if exist "%CLI_OUT%\KTWirzade.CLI.exe.config" (
    copy /Y "%CLI_OUT%\KTWirzade.CLI.exe.config" "%STANDALONE%\KTWirzade.CLI.exe.config" >nul 2>&1
    if errorlevel 1 set FALHAS=1
)
copy /Y "%GUI_OUT%\KTWirzade.GUI.exe" "%STANDALONE%\KTWirzade.GUI.exe" >nul 2>&1
if errorlevel 1 set FALHAS=1
copy /Y "%GUI_OUT%\KTWirzade.GUI.exe.config" "%STANDALONE%\KTWirzade.GUI.exe.config" >nul 2>&1
if errorlevel 1 set FALHAS=1
if exist "%GUI_OUT%\KTWirzade.Shared.dll.config" (
    copy /Y "%GUI_OUT%\KTWirzade.Shared.dll.config" "%STANDALONE%\KTWirzade.Shared.dll.config" >nul 2>&1
    if errorlevel 1 set FALHAS=1
)

rem APBX Developer: pasta completa (exe + todas as dependencias) para instalacao
set DEVTOOL_DEST=%STANDALONE%\APBX-Developer
if exist "%DEVTOOL_OUT%\KTWirzade.DevTool.exe" (
    if exist "%DEVTOOL_DEST%" rmdir /s /q "%DEVTOOL_DEST%"
    xcopy "%DEVTOOL_OUT%\*" "%DEVTOOL_DEST%\" /e /i /y >nul 2>&1
    if errorlevel 1 set FALHAS=1
    if exist "%DEVTOOL_DEST%\KTWirzade.DevTool.pdb" del "%DEVTOOL_DEST%\KTWirzade.DevTool.pdb" >nul 2>&1
)

rem APBX DevKit: pasta completa (exe + dependencias + 7za.exe de empacotamento)
set DEVKIT_OUT=%BASE%\KTWirzade.DevKit\bin\%PLATFORM%\%CONFIG%\net4.8-windows
set DEVKIT_DEST=%STANDALONE%\APBX-DevKit
if exist "%DEVKIT_OUT%\KTWirzade.DevKit.exe" (
    if exist "%DEVKIT_DEST%" rmdir /s /q "%DEVKIT_DEST%"
    xcopy "%DEVKIT_OUT%\*" "%DEVKIT_DEST%\" /e /i /y >nul 2>&1
    if errorlevel 1 set FALHAS=1
    if exist "%DEVKIT_DEST%\KTWirzade.DevKit.pdb" del "%DEVKIT_DEST%\KTWirzade.DevKit.pdb" >nul 2>&1
)

if "%FALHAS%"=="1" (
    echo [ERRO] Uma ou mais copias falharam na etapa 6/6.
    exit /b 1
)

echo.
echo Verificando alinhamento de versoes criticas (Shared x GUI x CLI)...
set VERSAO_INCORRETA=0
for %%d in (System.Text.Json.dll Microsoft.Win32.TaskScheduler.dll Polly.Core.dll SharpSevenZip.dll Newtonsoft.Json.dll JetBrains.Annotations.dll YamlDotNet.dll) do (
    if exist "%SHARED_BIN%\%%d" (
        if exist "%GUI_OUT%\%%d" (
            powershell -NoProfile -Command "$s=(Get-Item '%SHARED_BIN%\%%d').VersionInfo.ProductVersion; $g=(Get-Item '%GUI_OUT%\%%d').VersionInfo.ProductVersion; $c=if (Test-Path '%CLI_OUT%\%%d') { (Get-Item '%CLI_OUT%\%%d').VersionInfo.ProductVersion } else { $g }; Write-Host ('  - %%d : Shared=' + $s + '  GUI=' + $g + '  CLI=' + $c); if (($s -ne $g) -or ($s -ne $c)) { Write-Host ('    [DIVERGENTE]'); exit 1 }; exit 0"
            if errorlevel 1 set VERSAO_INCORRETA=1
        ) else (
            echo   [DIVERGENTE] %%d ausente no output do GUI.
            set VERSAO_INCORRETA=1
        )
    )
)

if "%VERSAO_INCORRETA%"=="1" goto :version_mismatch

echo.
echo ============================================
echo    Build concluido com sucesso!
echo ============================================
echo.
echo Arquivos gerados:
echo   CLI Standalone: %STANDALONE%\KTWirzade.CLI.exe
echo   GUI Standalone: %STANDALONE%\KTWirzade.GUI.exe
echo   APBX Developer: %STANDALONE%\APBX-Developer\
echo   CLI direto:     %CLI_OUT%\KTWirzade.CLI.exe
echo   GUI direto:     %GUI_OUT%\KTWirzade.GUI.exe
echo.
echo IMPORTANTE: Mantenha os pacotes NuGet alinhados entre os 3 projetos!
echo   - KTWirzade.Shared.csproj
echo   - KTWirzade.CLI.csproj
echo   - KTWirzade.GUI\src\KTWirzade.GUI.csproj
echo.
exit /b 0

:version_mismatch
echo.
echo [ERRO] Versoes de pacotes criticos divergem entre os projetos.
echo        Alinhe System.Text.Json, TaskScheduler, Polly.Core,
echo        SharpSevenZip, Newtonsoft.Json, JetBrains.Annotations, YamlDotNet.
exit /b 1
