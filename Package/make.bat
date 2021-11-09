@pushd %~dp0

dotnet publish -r win10-x64 -c Release --self-contained true -p:PublishTrimmed=false ..\DevComrade
@if errorlevel 1 goto :error
@goto :success

:error
@echo Error exit code: %errorlevel%
@popd
@exit /b 1

:success
@popd
@echo Run DevComrade from:
@where /r "%~dp0.." "DevComrade.exe"
@exit /b 0
