@pushd %~dp0
@call make.bat
@if errorlevel 1 goto :error

start ..\DevComrade\bin\Release\net10.0-windows\win-x64\publish\DevComrade.exe
@goto :success

:error
@echo Error exit code: %errorlevel%
@popd
@exit /b 1

:success
@popd
@exit /b 1
