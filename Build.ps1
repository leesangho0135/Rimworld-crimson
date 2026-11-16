param(
    [ValidateSet('ALL','CORE','SOS2_ADDON','FURNITURE','RECREATION','SANITARY','MEDICAL','DECORATION','PRODUCTION','EQUIPMENT')][string]$Project='ALL',
    [switch]$SkipCompile,
    [string]$RimWorldRoot='C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$SOS2Root='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\1909914131',
    [string]$HarmonyRoot='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077',
    [string]$VehicleFrameworkRoot='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3014915404',
    [string]$DubsBadHygieneRoot='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\836308268',
    [string]$VanillaExpandedFrameworkRoot='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2023507013',
    [string]$AutomaticHydroponicsRoot='C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3528490718'
)
$ErrorActionPreference='Stop'
$root=$PSScriptRoot
$run=Join-Path $root ('.artifacts\Build\'+(Get-Date -Format 'yyyyMMdd-HHmmss-fff'))
$package=Join-Path $run 'CrimsonSOS2'
New-Item -ItemType Directory -Path $run -Force | Out-Null
& python (Join-Path $root 'Tools\Assemble-Package.py') --root $root --output $package
if($LASTEXITCODE -ne 0){throw 'Package assembly failed'}
$current=Join-Path $root 'CrimsonSOS2'
if($SkipCompile){
 foreach($relative in @('1.6\Assemblies','Compatibility\DubsBadHygiene\Assemblies','Compatibility\AutomaticHydroponics\Assemblies')){
  $source=Join-Path $current $relative
  if(Test-Path -LiteralPath $source){New-Item -ItemType Directory -Path (Join-Path $package $relative) -Force | Out-Null;Copy-Item -Path ($source+'\*.dll') -Destination (Join-Path $package $relative)}
 }
 if(-not(Test-Path -LiteralPath (Join-Path $package '1.6\Assemblies\CrimsonSOS2.dll'))){throw 'No previous runtime: run without SkipCompile first'}
}else{
 & (Join-Path $root 'Tools\Build-Runtime.ps1') -RepositoryRoot $root -PackageRoot $package -RimWorldRoot $RimWorldRoot -SOS2Root $SOS2Root -HarmonyRoot $HarmonyRoot -VehicleFrameworkRoot $VehicleFrameworkRoot -DubsBadHygieneRoot $DubsBadHygieneRoot -VanillaExpandedFrameworkRoot $VanillaExpandedFrameworkRoot -AutomaticHydroponicsRoot $AutomaticHydroponicsRoot
}
if(Test-Path -LiteralPath $current){
 $old=Get-Item -LiteralPath $current -Force
 if(-not($old.Attributes -band [IO.FileAttributes]::ReparsePoint)){throw 'Refuse to replace a non-generated package directory'}
 [IO.Directory]::Delete($current)
}
New-Item -ItemType Junction -Path $current -Target $package | Out-Null
[ordered]@{status='built';selectedProject=$Project;package=$package;runtimeAssembly='CrimsonSOS2.dll';runtimeMode='integrated-compatibility-preserving';gameLaunched=$false;appliedToPlayingCopy=$false;time=(Get-Date -Format o)} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $run 'build-result.json') -Encoding UTF8
Write-Output ('Built project workspace '+$Project+'; integrated game package: '+$package)
