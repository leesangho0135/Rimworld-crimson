param([switch]$SkipCompile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '..\..\Build.ps1') -Project 'RECREATION' -SkipCompile:$SkipCompile
