@echo off
setlocal EnableExtensions

rem Optional first argument overrides the Steam client_pc folder.
if "%~1"=="" (
  set "GAME=C:\Program Files (x86)\Steam\steamapps\common\Warhammer 40000 Space Marine 2\client_pc"
) else (
  set "GAME=%~1"
)

set "HERE=%~dp0"
set "OUT=%HERE%extracted"

echo Extracting
echo   %GAME%
echo into
echo   %OUT%
echo.

if not exist "%HERE%LibSaber.SM2Extractor.exe" (
  echo LibSaber.SM2Extractor.exe was not found next to this script.
  goto :done
)

"%HERE%LibSaber.SM2Extractor.exe" extract "%GAME%" --output "%OUT%"
echo.
echo Finished with exit code %ERRORLEVEL%.

:done
echo.
pause
endlocal
