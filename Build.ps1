param(
    [string]$GamePath,
    [switch]$Install,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$projectPath = $PSScriptRoot
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
Copy-Item -LiteralPath (Join-Path $projectPath 'DEVELOPMENT.md') -Destination $packagePath -Force
if ($Test) {
    $workbenchTests = Join-Path $buildPath 'WorkbenchOrderTests.exe'
    & $compilerPath /nologo /target:exe "/out:$workbenchTests" (Join-Path $projectPath 'src\DependencyOrder.cs') (Join-Path $projectPath 'src\InventoryOrder.cs') (Join-Path $projectPath 'tests\WorkbenchOrderTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Workbench ordering tests could not compile.' }
    & $workbenchTests
    if ($LASTEXITCODE -ne 0) { throw 'Workbench ordering tests failed.' }
    $workbenchRulesTests = Join-Path $buildPath 'WorkbenchRulesTests.exe'
    & $compilerPath /nologo /target:exe "/out:$workbenchRulesTests" (Join-Path $projectPath 'src\WorkbenchRules.cs') (Join-Path $projectPath 'tests\WorkbenchRulesTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Workbench behavior tests could not compile.' }
    & $workbenchRulesTests
    if ($LASTEXITCODE -ne 0) { throw 'Workbench behavior tests failed.' }
    $workbenchContractTests = Join-Path $buildPath 'WorkbenchGameContractTests.exe'
    $cecilPath = Join-Path $GamePath 'BepInEx\core\Mono.Cecil.dll'
    Copy-Item -LiteralPath $cecilPath -Destination $buildPath -Force
    & $compilerPath /nologo /target:exe "/out:$workbenchContractTests" "/reference:$cecilPath" (Join-Path $projectPath 'tests\WorkbenchGameContractTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Game workbench API tests could not compile.' }
    & $workbenchContractTests $managedPath (Join-Path $packagePath 'PCBS-Helper.dll')
    if ($LASTEXITCODE -ne 0) { throw 'Game workbench API tests failed.' }
    $requirementsTests = Join-Path $buildPath 'RequirementClarificationsTests.exe'
    & $compilerPath /nologo /target:exe "/out:$requirementsTests" (Join-Path $projectPath 'src\RequirementClarifications.cs') (Join-Path $projectPath 'tests\RequirementClarificationsTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Job clarification tests could not compile.' }
    & $requirementsTests
    if ($LASTEXITCODE -ne 0) { throw 'Job clarification tests failed.' }
    $jobReaderTests = Join-Path $buildPath 'JobRequirementReaderTests.exe'
    & $compilerPath /nologo /target:exe "/out:$jobReaderTests" "/reference:$(Join-Path $managedPath 'Assembly-CSharp-firstpass.dll')" "/reference:$(Join-Path $managedPath 'UnityEngine.CoreModule.dll')" (Join-Path $projectPath 'src\RequirementClarifications.cs') (Join-Path $projectPath 'src\JobRequirementReader.cs') (Join-Path $projectPath 'tests\JobRequirementReaderTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Native job objective tests could not compile.' }
    & $jobReaderTests $managedPath
    if ($LASTEXITCODE -ne 0) { throw 'Native job objective tests failed.' }
    $positionTestExe = Join-Path $buildPath 'WindowPositionTests.exe'
    & $compilerPath /nologo /target:exe "/out:$positionTestExe" (Join-Path $projectPath 'src\WindowPositionMath.cs') (Join-Path $projectPath 'tests\WindowPositionTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Window position tests could not compile.' }
    & $positionTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Window position tests failed.' }
    $cursorTestExe = Join-Path $buildPath 'PanelCursorModeTests.exe'
    & $compilerPath /nologo /target:exe "/out:$cursorTestExe" (Join-Path $projectPath 'src\PanelCursorMode.cs') (Join-Path $projectPath 'tests\PanelCursorModeTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Cursor toggle tests could not compile.' }
    & $cursorTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Cursor toggle tests failed.' }
    $scrollTestExe = Join-Path $buildPath 'BrowserScrollBufferTests.exe'
    & $compilerPath /nologo /target:exe "/out:$scrollTestExe" (Join-Path $projectPath 'src\BrowserScrollBuffer.cs') (Join-Path $projectPath 'tests\BrowserScrollBufferTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Browser scroll tests could not compile.' }
    & $scrollTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Browser scroll tests failed.' }
    $emailLayoutTestExe = Join-Path $buildPath 'EmailButtonLayoutTests.exe'
    & $compilerPath /nologo /target:exe "/out:$emailLayoutTestExe" (Join-Path $projectPath 'src\EmailButtonLayout.cs') (Join-Path $projectPath 'tests\EmailButtonLayoutTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Email button layout tests could not compile.' }
    & $emailLayoutTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Email button layout tests failed.' }
    $clickPurchaseTestExe = Join-Path $buildPath 'ClickPurchaseCartTests.exe'
    & $compilerPath /nologo /target:exe "/out:$clickPurchaseTestExe" (Join-Path $projectPath 'src\ClickPurchaseCart.cs') (Join-Path $projectPath 'tests\ClickPurchaseCartTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Click purchase cart tests could not compile.' }
    & $clickPurchaseTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Click purchase cart tests failed.' }
    $buildOrderTestExe = Join-Path $buildPath 'CalculatorBuildOrderTests.exe'
    & $compilerPath /nologo /target:exe /main:CalculatorBuildOrderTests "/out:$buildOrderTestExe" "/reference:$(Join-Path $managedPath 'ZFBrowser.dll')" (Join-Path $projectPath 'src\ClickPurchaseCart.cs') (Join-Path $projectPath 'src\CalculatorBuildOrder.cs') (Join-Path $projectPath 'tests\ClickPurchaseCartTests.cs') (Join-Path $projectPath 'tests\CalculatorBuildOrderTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Calculator build order tests could not compile.' }
    & $buildOrderTestExe $managedPath (Join-Path $buildPath 'game-catalog\matching.json')
    if ($LASTEXITCODE -ne 0) { throw 'Calculator build order tests failed.' }
    $careerTestExe = Join-Path $buildPath 'CareerProgressTests.exe'
    & $compilerPath /nologo /target:exe "/out:$careerTestExe" (Join-Path $projectPath 'src\CareerProgress.cs') (Join-Path $projectPath 'tests\CareerProgressTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Career progress tests could not compile.' }
    & $careerTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Career progress tests failed.' }
    $localizationTestExe = Join-Path $buildPath 'HelperLocalizationTests.exe'
    & $compilerPath /nologo /target:exe "/out:$localizationTestExe" (Join-Path $projectPath 'src\HelperLocalization.cs') (Join-Path $projectPath 'tests\HelperLocalizationTests.cs')
    if ($LASTEXITCODE -ne 0) { throw 'Native localization tests could not compile.' }
    & $localizationTestExe
    if ($LASTEXITCODE -ne 0) { throw 'Native localization tests failed.' }
    $nodeCommand = Get-Command node -ErrorAction SilentlyContinue
    if ($nodeCommand) {
        & $nodeCommand.Source (Join-Path $projectPath 'tests\CalculatorStateTests.js')
        if ($LASTEXITCODE -ne 0) { throw 'Calculator state tests failed.' }
        & $nodeCommand.Source (Join-Path $projectPath 'tests\CalculatorJobRulesTests.js')
        if ($LASTEXITCODE -ne 0) { throw 'Calculator job rule tests failed.' }
        & $nodeCommand.Source (Join-Path $projectPath 'tests\CalculatorLocalizationTests.js')
        if ($LASTEXITCODE -ne 0) { throw 'Calculator localization tests failed.' }
        if (Test-Path -LiteralPath (Join-Path $projectPath 'build\game-catalog\helper-catalog.json')) {
            & (Join-Path $projectPath 'scripts\Test-Calculator.ps1') -GamePath $GamePath
        }
    } else { Write-Warning 'Node.js not found: calculator JavaScript tests were skipped.' }
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
