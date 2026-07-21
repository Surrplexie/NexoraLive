@echo off
set LOG=%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\beamng.log
set EVENTS=%LOCALAPPDATA%\NL\beamng-events.ndjson
echo === NL bridge check ===
echo.
echo Log file: %LOG%
findstr /i "NL_BeamNGBridge loaded Failed to append" "%LOG%" 2>nul
if errorlevel 1 echo (no NL lines found in log yet)
echo.
echo Events file: %EVENTS%
if exist "%EVENTS%" (type "%EVENTS%") else echo (events file missing - drive in-game after restart)
echo.
echo Zip modScript path check:
powershell -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::OpenRead('%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\mods\NL_BeamNGBridge.zip').Entries | Where-Object { $_.FullName -like '*modScript*' } | ForEach-Object { $_.FullName }"
pause
