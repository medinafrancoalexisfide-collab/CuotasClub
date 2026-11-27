@echo off
rem Genera el instalador de CuotasClub en la carpeta "salida".
rem Necesita: .NET 10 SDK (https://dot.net) e Inno Setup 6 (https://jrsoftware.org/isdl.php).
chcp 65001 >nul
cd /d "%~dp0.."

echo.
echo === 1/2 Preparando el programa (puede tardar un par de minutos) ===
if exist publicado rmdir /s /q publicado
dotnet publish src\CuotasClub\CuotasClub.csproj -c Release -r win-x64 --self-contained true -o publicado
if errorlevel 1 goto error

echo.
echo === 2/2 Armando el instalador ===
set ISCC="%ProgramFiles(x86)%\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="%ProgramFiles%\Inno Setup 6\ISCC.exe"
if not exist %ISCC% set ISCC="%LOCALAPPDATA%\Programs\Inno Setup 6\ISCC.exe"
if not exist %ISCC% (
  echo No se encontro Inno Setup 6. Instalalo desde https://jrsoftware.org/isdl.php y volve a probar.
  goto error
)
%ISCC% instalador\CuotasClub.iss
if errorlevel 1 goto error

echo.
echo Listo. El instalador esta en la carpeta "salida".
explorer salida
pause
exit /b 0

:error
echo.
echo Algo salio mal. Copia lo que dice arriba y pasaselo a Claude.
pause
exit /b 1
