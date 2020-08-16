@cd %~dp0
dotnet publish -r win10-x64 -c Release --self-contained false -p:PublishTrimmed=false ..\DevComrade
@if errorlevel 1 goto :error
start ..\DevComrade\bin\Release\netcoreapp3.1\win10-x64\DevComrade.exe
@goto :ok

:error
@echo Error exit code: %errorlevel%
@exit %errorlevel%

:ok
