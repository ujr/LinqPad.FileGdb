@echo off
rem Push built NuGet packages to NuGet.org
cd /d "%~dp0"
echo When prompted for API key: use your NuGet.org API key
rem Adjust VERSION as needed
set VERSION=0.1.0-beta1
set NUGET=nuget.exe
rem The NuGet gallery source URL:
set NUGET_SOURCE=https://api.nuget.org/v3/index.json
rem The NuGet integration source URL:
rem set NUGET_SOURCE=https://apiint.nugettest.org/v3/index.json
rem Symbol package (.snupkg) is automatically also pushed if it exists
%NUGET% push .\pkgs\FileGDB.Core.%VERSION%.nupkg -Source %NUGET_SOURCE% -SkipDuplicate
%NUGET% push .\pkgs\FileGDB.LinqPadDriver.%VERSION%.nupkg -Source %NUGET_SOURCE% -SkipDuplicate
pause
