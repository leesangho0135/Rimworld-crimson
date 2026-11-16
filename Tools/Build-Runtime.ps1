param(
    [Parameter(Mandatory=$true)][string]$RepositoryRoot,
    [Parameter(Mandatory=$true)][string]$PackageRoot,
    [string]$RimWorldRoot = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$SOS2Root = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\1909914131',
    [string]$HarmonyRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077',
    [string]$VehicleFrameworkRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3014915404',
    [string]$DubsBadHygieneRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\836308268',
    [string]$VanillaExpandedFrameworkRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2023507013',
    [string]$AutomaticHydroponicsRoot = 'C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\3528490718'
)

$ErrorActionPreference = 'Stop'
$workspaceRoot = $RepositoryRoot
$managedRoot = Join-Path $RimWorldRoot 'RimWorldWin64_Data\Managed'
$gameAssembly = Join-Path $managedRoot 'Assembly-CSharp.dll'
$unityAssembly = Join-Path $managedRoot 'UnityEngine.CoreModule.dll'
$unityImguiAssembly = Join-Path $managedRoot 'UnityEngine.IMGUIModule.dll'
$unityMathematicsAssembly = Join-Path $managedRoot 'Unity.Mathematics.dll'
$netstandardAssembly = Join-Path $managedRoot 'netstandard.dll'
$runtimeAssembly = Join-Path $managedRoot 'mscorlib.dll'
$systemAssembly = Join-Path $managedRoot 'System.dll'
$systemCoreAssembly = Join-Path $managedRoot 'System.Core.dll'
$systemXmlAssembly = Join-Path $managedRoot 'System.Xml.dll'
$sosAssembly = Join-Path $SOS2Root '1.6\Assemblies\ShipsHaveInsides.dll'
$harmonyAssembly = Join-Path $HarmonyRoot '1.6\Assemblies\0Harmony.dll'
$vehicleAssembly = Join-Path $VehicleFrameworkRoot '1.6\Assemblies\Vehicles.dll'
$smashToolsAssembly = Join-Path $VehicleFrameworkRoot '1.6\Assemblies\SmashTools.dll'
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'

foreach ($requiredPath in @($compilerPath, $gameAssembly, $unityAssembly, $unityImguiAssembly, $unityMathematicsAssembly, $netstandardAssembly, $runtimeAssembly, $systemAssembly, $systemCoreAssembly, $sosAssembly, $harmonyAssembly, $vehicleAssembly, $smashToolsAssembly)) {
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        throw "Required existing build input was not found: $requiredPath"
    }
}

$catalog = Get-Content -LiteralPath (Join-Path $workspaceRoot 'MOD_PROJECTS\projects.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$sourceFiles = @(foreach ($project in $catalog.projects) {
    $folder = Join-Path $workspaceRoot ('MOD_PROJECTS\' + $project.path)
    $main = Join-Path $folder 'Source\Main'
    if (Test-Path -LiteralPath $main) {
        Get-ChildItem -LiteralPath $main -Filter '*.cs' -File -Recurse | ForEach-Object { $_.FullName }
    }
})
$sourceFiles = @($sourceFiles | Sort-Object { [IO.Path]::GetFileName($_) })
if ($sourceFiles.Count -eq 0) {
    throw "No C# source files found: $sourceRoot"
}

$outputRoot = Join-Path $PackageRoot '1.6\Assemblies'
[void](New-Item -ItemType Directory -Path $outputRoot -Force)
$outputPath = Join-Path $outputRoot 'CrimsonSOS2.dll'
$compilerArguments = @(
    '/noconfig',
    '/nologo',
    '/nostdlib+',
    '/target:library',
    '/optimize+',
    '/warn:4',
    '/warnaserror+',
    '/utf8output',
    '/codepage:65001',
    "/out:$outputPath",
    "/reference:$runtimeAssembly",
    "/reference:$systemAssembly",
    "/reference:$systemCoreAssembly",
    "/reference:$systemXmlAssembly",
    "/reference:$gameAssembly",
    "/reference:$unityAssembly",
    "/reference:$unityImguiAssembly",
    "/reference:$unityMathematicsAssembly",
    "/reference:$netstandardAssembly",
    "/reference:$sosAssembly",
    "/reference:$harmonyAssembly",
    "/reference:$vehicleAssembly",
    "/reference:$smashToolsAssembly"
) + $sourceFiles

& $compilerPath @compilerArguments
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Write-Output "Built $outputPath"

# The bath renderer is loaded only with Dubs. Keep that reference out of the
# main addon so storage and the existing ship components remain independent.
$hygieneSource = Join-Path $workspaceRoot 'MOD_PROJECTS\SANITARY\Source\DubsBadHygiene'
if (Test-Path -LiteralPath $hygieneSource -PathType Container) {
    $hygieneAssembly = Join-Path $DubsBadHygieneRoot '1.6\Assemblies\BadHygiene.dll'
    if (Test-Path -LiteralPath $hygieneAssembly -PathType Leaf) {
        $hygieneSources = @(Get-ChildItem -LiteralPath $hygieneSource -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
        $hygieneOutputRoot = Join-Path $PackageRoot 'Compatibility\DubsBadHygiene\Assemblies'
        [void](New-Item -ItemType Directory -Path $hygieneOutputRoot -Force)
        $hygieneOutput = Join-Path $hygieneOutputRoot 'CrimsonSOS2.DubsBadHygiene.dll'
        $hygieneArguments = @($compilerArguments | Where-Object { -not $_.StartsWith('/out:') -and -not $_.EndsWith('.cs') })
        $hygieneArguments += @("/out:$hygieneOutput", "/reference:$outputPath")
        foreach ($name in @('BadHygiene.dll', '0DubCore.dll', '0MultiplayerAPI.dll')) {
            $reference = Join-Path $DubsBadHygieneRoot ('1.6\Assemblies\' + $name)
            if (Test-Path -LiteralPath $reference -PathType Leaf) { $hygieneArguments += '/reference:' + $reference }
        }
        $hygieneArguments += $hygieneSources
        & $compilerPath @hygieneArguments
        if ($LASTEXITCODE -ne 0) { throw "Optional hygiene renderer compilation failed with exit code $LASTEXITCODE." }
        Write-Output "Built $hygieneOutput"
    } else {
        Write-Output 'Skipped optional hygiene renderer: Dubs Bad Hygiene is not installed.'
    }
}

# Wall reports reuse the public furniture renderer while remaining deployable
# without replacing the main assembly or including unrelated pending changes.
$reportsSource = Join-Path $workspaceRoot 'MOD_PROJECTS\SOS2_ADDON\Source\WallReports'
if (Test-Path -LiteralPath $reportsSource -PathType Container) {
    $reportsOutput = Join-Path $outputRoot 'CrimsonSOS2.WallReports.dll'
    $reportsArguments = @($compilerArguments | Where-Object { -not $_.StartsWith('/out:') -and -not $_.EndsWith('.cs') })
    $reportsArguments += @("/out:$reportsOutput", "/reference:$outputPath")
    $reportsArguments += @(Get-ChildItem -LiteralPath $reportsSource -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
    & $compilerPath @reportsArguments
    if ($LASTEXITCODE -ne 0) { throw "Wall reports compilation failed with exit code $LASTEXITCODE." }
    Write-Output "Built $reportsOutput"
}

# Automatic Hydroponics keeps its native VEF processor. Its visual adapter is
# built separately so the main addon never requires PipeSystem at load time.
$hydroSource = Join-Path $workspaceRoot 'MOD_PROJECTS\PRODUCTION\Source\AutomaticHydroponics'
$pipeAssembly = Join-Path $VanillaExpandedFrameworkRoot '1.6\Assemblies\PipeSystem.dll'
$hydroMetadata = Join-Path $AutomaticHydroponicsRoot 'About\About.xml'
if (Test-Path -LiteralPath $hydroSource -PathType Container) {
    if ((Test-Path -LiteralPath $pipeAssembly -PathType Leaf) -and (Test-Path -LiteralPath $hydroMetadata -PathType Leaf)) {
        $hydroSources = @(Get-ChildItem -LiteralPath $hydroSource -Filter '*.cs' -File | Sort-Object Name | ForEach-Object { $_.FullName })
        $hydroOutputRoot = Join-Path $PackageRoot 'Compatibility\AutomaticHydroponics\Assemblies'
        [void](New-Item -ItemType Directory -Path $hydroOutputRoot -Force)
        $hydroOutput = Join-Path $hydroOutputRoot 'CrimsonSOS2.AutomaticHydroponics.dll'
        $hydroArguments = @($compilerArguments | Where-Object { -not $_.StartsWith('/out:') -and -not $_.EndsWith('.cs') })
        $hydroArguments += @("/out:$hydroOutput", "/reference:$outputPath", "/reference:$pipeAssembly")
        $hydroArguments += $hydroSources
        & $compilerPath @hydroArguments
        if ($LASTEXITCODE -ne 0) { throw "Optional hydroponics adapter compilation failed with exit code $LASTEXITCODE." }
        Write-Output "Built $hydroOutput"
    } else {
        Write-Output 'Skipped optional hydroponics adapter: Automatic Hydroponics or VEF is not installed.'
    }
}
