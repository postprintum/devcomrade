@cd %~dp0
pwsh -f make.ps1
@if errorlevel 1 goto :error
start ..\DevComrade\bin\Release\netcoreapp3.1\win10-x64\DevComrade.exe
@goto :ok

:error
@echo Error exit code: %errorlevel%
@exit %errorlevel%

:ok
