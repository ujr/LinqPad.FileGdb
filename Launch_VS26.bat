@cd /d "%~dp0"

@rem TargetFramework=net10.0-windows

@rem VS2022: C:\Program Files\Microsoft Visual Studio\2022\Professional\Common7\IDE\devenv.exe
set DEVENV="C:\Program Files\Microsoft Visual Studio\18\Professional\Common7\IDE\devenv.exe"
set DEVOPTS=
@REM set DEVOPTS=/ReSharper.LogFile C:\Temp\ReSharper_Log.txt /ReSharper.LogLevel Verbose

%DEVENV% %DEVOPTS% src\FileGDB.sln
