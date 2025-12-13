@rem Clear all /bin/ dirs, removing stale state
cd /d "%~dp0"
git clean -xdf **/bin/**
pause
