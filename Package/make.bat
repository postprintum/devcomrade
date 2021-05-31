@pushd %~dp0

dotnet publish -r win10-x64 -c Release --self-contained false -p:PublishTrimmed=false ..\DevComrade
@if errorlevel 1 goto :error
@goto :success

:error
@echo Error exit code: %errorlevel%
@popd
@exit /b 1

:success
@popd
@echo Run DevComrade from:
@echo "%~dp0..\DevComrade\bin\Release\net5.0-windows7\win10-x64\DevComrade.exe"
@exit /b 0
