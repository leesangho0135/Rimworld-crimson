param([switch]$SkipCompile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '..\..\Build.ps1') -Project 'SOS2_ADDON' -SkipCompile:$SkipCompile
