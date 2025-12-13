@rem Clear all /obj/ dirs, removing stale nuget pkg refs
cd /d "%~dp0"
git clean -xdf **/obj/**
pause
