param(
    [string]$GamePath,
    [string]$CatalogPath = (Join-Path $PSScriptRoot '..\build\game-catalog\helper-catalog.json')
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
if (-not $GamePath) { $GamePath = (Get-Content (Join-Path $projectPath 'build.local.json') -Raw | ConvertFrom-Json).gamePath }
$managedPath = Join-Path $GamePath 'PCBS_Data\Managed'
$catalog = (Resolve-Path -LiteralPath $CatalogPath).Path
$fixturePath = Join-Path $projectPath 'build\game-catalog\helper-catalog.json'
if ($catalog -ne $fixturePath) { Copy-Item -LiteralPath $catalog -Destination $fixturePath -Force }
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$oracle = Join-Path $projectPath 'build\GameScoreOracle.exe'
$references = @('Assembly-CSharp-firstpass.dll','UnityEngine.dll','UnityEngine.CoreModule.dll','ZFBrowser.dll') | ForEach-Object { '/reference:' + (Join-Path $managedPath $_) }
& $compiler /nologo /target:exe "/out:$oracle" $references (Join-Path $projectPath 'tests\GameScoreOracle.cs')
if ($LASTEXITCODE -ne 0) { throw 'Game score oracle could not compile.' }
& $oracle $managedPath $fixturePath (Join-Path $projectPath 'build\game-catalog\game-score-oracle.json')
if ($LASTEXITCODE -ne 0) { throw 'Game score oracle failed.' }
$node = Get-Command node -ErrorAction Stop
& $node.Source (Join-Path $projectPath 'tests\HelperCalculatorEngineTests.js')
if ($LASTEXITCODE -ne 0) { throw 'Independent calculator tests failed.' }
