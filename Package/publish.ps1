#Requires -Version 6.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

#TODO: make a Chocolatey package

# NB: trimming is not supported for Windows Forms, see https://aka.ms/dotnet-illink/windows-forms
dotnet clean -c Release ..\DevComrade
dotnet publish -r win-x64 -c Release --self-contained true -p:PublishTrimmed=false ..\DevComrade
