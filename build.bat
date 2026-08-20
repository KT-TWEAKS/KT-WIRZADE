@echo off
setlocal enabledelayedexpansion

echo ============================================
echo    KT WIRZADE - Build Script v0.8.5
echo ============================================
echo.

set MSBUILD=C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\amd64\MSBuild.exe
set DOTNET=C:\dotnet-sdk\dotnet.exe
set BASE=%~dp0
set CONFIG=Release
set PLATFORM=x64

if "%BASE:~-1%"=="\" set BASE=%BASE:~0,-1%

if not exist "%MSBUILD%" (
    echo [ERRO] MSBuild nao encontrado.
    exit /b 1
)
if not exist "%DOTNET%" (
    echo [ERRO] dotnet SDK nao encontrado.
    exit /b 1
)

echo [1/7] Preparando client-helper.dll nativo (necessario se nao houver compilador C++)...
if not exist "%BASE%\Core\Helper\x64\Release\client-helper.dll" (
    if exist "%BASE%\KTWirzade.GUI\src\Resources\client-helper.dll" (
        if not exist "%BASE%\Core\Helper\x64\Release" mkdir "%BASE%\Core\Helper\x64\Release"
        copy /Y "%BASE%\KTWirzade.GUI\src\Resources\client-helper.dll" "%BASE%\Core\Helper\x64\Release\client-helper.dll" >nul
        echo   - client-helper.dll copiado para Core\Helper\x64\Release\
    ) else (
        echo   [AVISO] client-helper.dll nao encontrado em Resources. Build pode falhar.
    )
)

echo.
echo [2/7] Restaurando pacotes NuGet...
"%MSBUILD%" "%BASE%\KT-Wirzade.sln" /t:Restore /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /p:SolutionDir="%BASE%\\" /v:minimal
if errorlevel 1 (
    echo [ERRO] Falha ao restaurar pacotes NuGet.
    exit /b 1
)

echo.
echo [3/7] Compilando Shared + CLI (MSBuild)...
"%MSBUILD%" "%BASE%\KT-Wirzade.sln" /t:Build /p:Configuration=%CONFIG% /p:Platform=%PLATFORM% /p:SolutionDir="%BASE%\\" /m /v:minimal
if errorlevel 1 (
    echo [ERRO] Falha ao compilar Shared + CLI.
    exit /b 1
)

echo.
echo [4/7] Copiando Shared.dll e dependencias para Resources do GUI...
set SHARED_BIN=%BASE%\KTWirzade.Shared\bin\%PLATFORM%\%CONFIG%
set RES=%BASE%\KTWirzade.GUI\src\Resources
copy /Y "%SHARED_BIN%\KTWirzade.Shared.dll" "%RES%\KTWirzade.Shared.dll" >nul
copy /Y "%SHARED_BIN%\YamlDotNet.dll" "%RES%\YamlDotNet.dll" >nul
copy /Y "%SHARED_BIN%\TimeZoneConverter.dll" "%RES%\TimeZoneConverter.dll" >nul
copy /Y "%SHARED_BIN%\JetBrains.Annotations.dll" "%RES%\JetBrains.Annotations.dll" >nul
for %%f in ("%SHARED_BIN%\*.dll") do copy /Y "%%f" "%RES%\" >nul 2>&1
if errorlevel 1 (
    echo [ERRO] Falha ao copiar Shared.dll e dependencias.
    exit /b 1
)

echo.
echo [5/7] Compilando GUI (dotnet)...
"%DOTNET%" build "%BASE%\KTWirzade.GUI\src\KTWirzade.GUI.csproj" -c %CONFIG% -p:Platform=%PLATFORM% -p:SolutionDir="%BASE%\\" -v minimal
if errorlevel 1 (
    echo [ERRO] Falha ao compilar GUI.
    exit /b 1
)

echo.
echo [6/7] Copiando dependencias Shared para output do GUI e CLI-Standalone...
set GUI_OUT=%BASE%\KTWirzade.GUI\src\bin\%PLATFORM%\%CONFIG%\net4.8-windows
set CLI_OUT=%BASE%\KTWirzade.CLI\bin\%PLATFORM%\%CONFIG%
set STANDALONE=%BASE%\CLI-Standalone

for %%f in ("%SHARED_BIN%\*.dll") do (
    if exist "%%f" (
        copy /Y "%%f" "%GUI_OUT%\" >nul 2>&1
        copy /Y "%%f" "%CLI_OUT%\" >nul 2>&1
        copy /Y "%%f" "%STANDALONE%\" >nul 2>&1
    )
)
copy /Y "%CLI_OUT%\KTWirzade.CLI.exe" "%STANDALONE%\KTWirzade.CLI.exe" >nul 2>&1
copy /Y "%CLI_OUT%\KTWirzade.CLI.exe.config" "%STANDALONE%\KTWirzade.CLI.exe.config" >nul 2>&1
copy /Y "%GUI_OUT%\KTWirzade.GUI.exe" "%STANDALONE%\KTWirzade.GUI.exe" >nul 2>&1
copy /Y "%GUI_OUT%\KTWirzade.GUI.exe.config" "%STANDALONE%\KTWirzade.GUI.exe.config" >nul 2>&1
copy /Y "%GUI_OUT%\KTWirzade.Shared.dll.config" "%STANDALONE%\KTWirzade.Shared.dll.config" >nul 2>&1

echo.
echo [7/7] Verificando alinhamento de versoes criticas...
set VERSAO_INCORRETA=0
for %%d in (System.Text.Json.dll Microsoft.Win32.TaskScheduler.dll Polly.Core.dll SharpSevenZip.dll Newtonsoft.Json.dll) do (
    if exist "%SHARED_BIN%\%%d" (
        for /f "tokens=*" %%v in ('powershell -NoProfile -Command "(Get-Item '%SHARED_BIN%\%%d').VersionInfo.ProductVersion"') do (
            echo   - %%d : %%v
        )
    )
)

echo.
echo ============================================
echo    Build concluido com sucesso!
echo ============================================
echo.
echo Arquivos gerados:
echo   CLI Standalone: %STANDALONE%\KTWirzade.CLI.exe
echo   GUI Standalone: %STANDALONE%\KTWirzade.GUI.exe
echo   CLI direto:     %CLI_OUT%\KTWirzade.CLI.exe
echo   GUI direto:     %GUI_OUT%\KTWirzade.GUI.exe
echo.
echo IMPORTANTE: Mantenha os pacotes NuGet alinhados entre os 3 projetos!
echo   - KTWirzade.Shared.csproj
echo   - KTWirzade.CLI.csproj
echo   - KTWirzade.GUI\src\KTWirzade.GUI.csproj
echo.
