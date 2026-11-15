param(
    [string]$GamePath,
    [switch]$Install,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
$localTestRunner = Join-Path $projectPath 'tests\Run-Tests.ps1'
if ($Test -and -not (Test-Path -LiteralPath $localTestRunner)) {
    throw 'The local test suite is not included in the public repository. Build without -Test.'
}
if (-not $GamePath) {
    $localSettings = Join-Path $projectPath 'build.local.json'
    if (Test-Path -LiteralPath $localSettings) { $GamePath = (Get-Content -LiteralPath $localSettings -Raw | ConvertFrom-Json).gamePath }
    elseif ($env:PCBS_GAME_PATH) { $GamePath = $env:PCBS_GAME_PATH }
    else { throw 'Pass -GamePath with your PC Building Simulator installation directory.' }
}
$GamePath = (Resolve-Path -LiteralPath $GamePath).Path
$managedPath = Join-Path $GamePath 'PCBS_Data\Managed'
$pluginPath = Join-Path $GamePath 'BepInEx\plugins\PCBS-Helper'
$buildPath = Join-Path $projectPath 'build'
$packagePath = Join-Path $buildPath 'package\PCBS-Helper'
New-Item -ItemType Directory -Path $packagePath -Force | Out-Null
$compilerPath = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compilerPath)) { throw 'Windows .NET Framework C# compiler not found.' }
$referenceFiles = @('mscorlib.dll','System.dll','System.Core.dll','UnityEngine.dll',
    'UnityEngine.CoreModule.dll','UnityEngine.PhysicsModule.dll','UnityEngine.IMGUIModule.dll','UnityEngine.InputModule.dll',
    'UnityEngine.TextRenderingModule.dll','UnityEngine.UIModule.dll','UnityEngine.UI.dll','Assembly-CSharp-firstpass.dll',
    'ZFBrowser.dll','Rewired_Core.dll') | ForEach-Object { Join-Path $managedPath $_ }
$referenceFiles += (Join-Path $GamePath 'BepInEx\core\BepInEx.dll'), (Join-Path $GamePath 'BepInEx\core\0Harmony.dll')
foreach ($referenceFile in $referenceFiles) {
    if (-not (Test-Path -LiteralPath $referenceFile)) { throw "Required game/mod reference missing: $referenceFile" }
}
$compilerArguments = @('/noconfig','/nologo','/target:library','/nostdlib+','/optimize+','/platform:anycpu')
$compilerArguments += '/out:' + (Join-Path $packagePath 'PCBS-Helper.dll')
$compilerArguments += $referenceFiles | ForEach-Object { '/reference:' + $_ }
$assetNames = @('helper-calculator.html','helper-calculator-engine.js','helper-calculator-app.js','embedded-ui.js','calculator-state-codec.js','calculator-state.js','calculator-job-rules.js','calculator-localization.js','calculator-build-order.js')
foreach ($assetName in $assetNames) {
    $assetFile = Join-Path $projectPath ('assets\' + $assetName)
    if (-not (Test-Path -LiteralPath $assetFile)) { throw "Calculator asset missing: $assetName." }
    $compilerArguments += '/resource:' + $assetFile + ',PCBSHelper.Assets.' + $assetName
}
$compilerArguments += (Get-ChildItem -LiteralPath (Join-Path $projectPath 'src') -Filter *.cs).FullName
& $compilerPath @compilerArguments
if ($LASTEXITCODE -ne 0) { throw "Helper compilation failed ($LASTEXITCODE)." }
# Remove only the obsolete named build copies. Sources and installed/user files stay intact.
foreach ($assetName in $assetNames) {
    $obsoleteFile = Join-Path $packagePath $assetName
    if (Test-Path -LiteralPath $obsoleteFile) { Remove-Item -LiteralPath $obsoleteFile }
}
foreach ($readmeName in @('README.md', 'README_EN.md', 'README.txt', 'README_EN.txt')) {
    Copy-Item -LiteralPath (Join-Path $projectPath $readmeName) -Destination $packagePath -Force
}
Copy-Item -LiteralPath (Join-Path $projectPath 'CHANGELOG.md') -Destination $packagePath -Force
# Retire the internal developer notes from previous local package builds.
$obsoleteDeveloperNotes = Join-Path $packagePath 'DEVELOPMENT.md'
if (Test-Path -LiteralPath $obsoleteDeveloperNotes) { Remove-Item -LiteralPath $obsoleteDeveloperNotes }
if ($Test) {
    & $localTestRunner -ProjectPath $projectPath -GamePath $GamePath -ManagedPath $managedPath `
        -CompilerPath $compilerPath -BuildPath $buildPath -PackagePath $packagePath
}
if ($Install) {
    if (Get-Process PCBS -ErrorAction SilentlyContinue) { throw 'Build succeeded. Close PCBS before installation, then rerun with -Install.' }
    New-Item -ItemType Directory -Path $pluginPath -Force | Out-Null
    $oldPlugin = Join-Path $pluginPath 'PCBS-Helper.dll'
    $backupDir = Join-Path $buildPath ('backups\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
    if (Test-Path -LiteralPath $oldPlugin) {
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Copy-Item -LiteralPath $oldPlugin -Destination $backupDir
    }
    Get-ChildItem -LiteralPath $packagePath -File | Copy-Item -Destination $pluginPath -Force
    # Retire the private development filename when updating the local test install.
    $developmentDll = Join-Path $GamePath 'BepInEx\plugins\PCBSCalculator\PCBSCalculator.dll'
    if (Test-Path -LiteralPath $developmentDll) {
        $resolvedDevelopment = (Resolve-Path -LiteralPath $developmentDll).Path
        if (-not $resolvedDevelopment.StartsWith($GamePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Development helper path is outside the selected game installation.'
        }
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Move-Item -LiteralPath $resolvedDevelopment -Destination (Join-Path $backupDir 'PCBSCalculator.dll')
    }
    # The four retained tweaks are now native to this DLL; disable the old F1 plugin.
    $legacyCheatDll = Join-Path $GamePath 'BepInEx\plugins\Cheat Menu\CheatMod.dll'
    if (Test-Path -LiteralPath $legacyCheatDll) {
        $resolvedLegacy = (Resolve-Path -LiteralPath $legacyCheatDll).Path
        if (-not $resolvedLegacy.StartsWith($GamePath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'Legacy Cheat Menu path is outside the selected game installation.'
        }
        New-Item -ItemType Directory -Path $backupDir -Force | Out-Null
        Move-Item -LiteralPath $resolvedLegacy -Destination (Join-Path $backupDir 'CheatMod.dll')
        Write-Output "Old CheatMod.dll backed up outside plugins: $backupDir"
    }
    Write-Output "Installed PCBS Helper in $pluginPath"
}
Write-Output "Built package: $packagePath"
