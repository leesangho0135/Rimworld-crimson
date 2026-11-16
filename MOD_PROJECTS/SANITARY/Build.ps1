param([switch]$SkipCompile)
$ErrorActionPreference='Stop'
& (Join-Path $PSScriptRoot '..\..\Build.ps1') -Project 'SANITARY' -SkipCompile:$SkipCompile
