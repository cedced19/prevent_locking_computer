@echo off
rem ============================================================
rem  Builds dist\KeepAwake.exe with the C# compiler that ships
rem  with every Windows installation (no .NET SDK required).
rem ============================================================
setlocal
set "CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
if not exist "%CSC%" set "CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
if not exist "%CSC%" (
    echo ERROR: .NET Framework C# compiler not found. 1>&2
    exit /b 1
)
if not exist "%~dp0dist" mkdir "%~dp0dist"

"%CSC%" /nologo /target:winexe /optimize+ /win32icon:"%~dp0assets\KeepAwake.ico" /out:"%~dp0dist\KeepAwake.exe" /r:System.Windows.Forms.dll /r:System.Drawing.dll "%~dp0src\KeepAwake\Program.cs"
if errorlevel 1 (
    echo Build FAILED. 1>&2
    exit /b 1
)
echo Built dist\KeepAwake.exe
endlocal
