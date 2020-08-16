#Requires -Version 6.0
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Set-Location $PSScriptRoot

#TODO: make a Chocolatey package

dotnet clean
dotnet publish -r win10-x64 -c Release --self-contained true -p:PublishTrimmed=true ..\DevComrade
