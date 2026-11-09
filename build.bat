@echo off
rem Builds Stickies_Window.exe with the C# compiler that comes with Windows.
rem Nothing to install. Double-click this file.

set CSC=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe
if not exist "%CSC%" set CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe
if not exist "%CSC%" (
  echo Could not find csc.exe. .NET Framework 4.8 is required.
  pause
  exit /b 1
)

cd /d "%~dp0"
"%CSC%" /nologo /target:winexe /optimize+ /out:Stickies_Window.exe ^
  /r:System.dll /r:System.Core.dll /r:System.Drawing.dll ^
  /r:System.Windows.Forms.dll /r:System.Xml.dll ^
  src\*.cs

if errorlevel 1 (
  echo.
  echo Build failed. See the errors above.
) else (
  echo.
  echo Done: Stickies_Window.exe
)
pause
