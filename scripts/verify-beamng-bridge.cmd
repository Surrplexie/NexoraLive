@echo off
set LOG=%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\beamng.log
set EVENTS=%LOCALAPPDATA%\NL\beamng-events.ndjson
set EVENTS_REAL=%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\NL\beamng-events.ndjson
echo === NL bridge check ===
echo.
echo Log file: %LOG%
findstr /i "NL_BeamNGBridge Events path writable Failed append Ignoring external" "%LOG%" 2>nul
if errorlevel 1 echo (no NL lines found in log yet)
echo.
echo Events link (Session Host): %EVENTS%
echo Events real (BeamNG writes): %EVENTS_REAL%
if exist "%EVENTS%" (type "%EVENTS%") else echo (events file missing)
echo.
echo Zip modScript path check:
powershell -NoProfile -Command "Add-Type -AssemblyName System.IO.Compression.FileSystem; [IO.Compression.ZipFile]::OpenRead('%LOCALAPPDATA%\BeamNG\BeamNG.drive\current\mods\NL_BeamNGBridge.zip').Entries | Where-Object { $_.FullName -like '*modScript*' } | ForEach-Object { $_.FullName }"
pause
