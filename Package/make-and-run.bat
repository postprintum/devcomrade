@pushd %~dp0
@call make.bat
@if errorlevel 1 goto :error

start ..\DevComrade\bin\Release\net5.0-windows7\win10-x64\DevComrade.exe
@goto :success

:error
@echo Error exit code: %errorlevel%
@popd
@exit /b 1

:success
@popd
@exit /b 1
