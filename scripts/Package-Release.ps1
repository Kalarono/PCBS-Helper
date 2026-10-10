param(
    [string]$GamePath,
    [string]$BepInExArchivePath,
    [switch]$Test
)
$ErrorActionPreference = 'Stop'
$projectPath = Split-Path -Parent $PSScriptRoot
$bepInExName = 'BepInEx_win_x64_5.4.23.5.zip'
$bepInExHash = '82F9878551030F54657792C0740D9D51A09500EEAE1FBA21106B0C441E6732C4'
$bepInExUrl = 'https://github.com/BepInEx/BepInEx/releases/download/v5.4.23.5/' + $bepInExName

if (-not $BepInExArchivePath) {
    $dependencyPath = Join-Path $projectPath 'build\dependencies'
    New-Item -ItemType Directory -Path $dependencyPath -Force | Out-Null
    $BepInExArchivePath = Join-Path $dependencyPath $bepInExName
    if (-not (Test-Path -LiteralPath $BepInExArchivePath)) {
        Invoke-WebRequest -Uri $bepInExUrl -OutFile $BepInExArchivePath
    }
}
$BepInExArchivePath = (Resolve-Path -LiteralPath $BepInExArchivePath).Path
if ((Get-FileHash -LiteralPath $BepInExArchivePath -Algorithm SHA256).Hash -ne $bepInExHash) {
    throw 'BepInEx archive does not match the official Windows x64 5.4.23.5 release.'
}

& (Join-Path $projectPath 'Build.ps1') -GamePath $GamePath -Test:$Test
$packagePath = Join-Path $projectPath 'build\package\PCBS-Helper'
$pluginSource = Get-Content -LiteralPath (Join-Path $projectPath 'src\PCBSCalculator.cs') -Raw
$versionMatch = [regex]::Match($pluginSource, 'BepInPlugin\("local\.pcbs\.calculator", "PCBS-Helper", "([0-9.]+)"\)')
if (-not $versionMatch.Success) { throw 'Cannot read the helper version.' }
$version = $versionMatch.Groups[1].Value
$fullArchiveName = "PCBS-Helper-$version-Full.zip"
$readmeNames = @('README.txt', 'README_EN.txt')
foreach ($readmeName in $readmeNames) {
    $readmeSource = Join-Path $projectPath $readmeName
    $readmeText = Get-Content -LiteralPath $readmeSource -Raw
    if (-not $readmeText.Contains("PCBS-Helper $version") -or -not $readmeText.Contains($fullArchiveName)) {
        throw "Update $readmeName to the current helper version before packaging."
    }
}
$releasePath = Join-Path $projectPath "build\release\$version"
New-Item -ItemType Directory -Path $releasePath -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $packagePath 'PCBS-Helper.dll') -Destination $releasePath -Force
Copy-Item -LiteralPath $BepInExArchivePath -Destination (Join-Path $releasePath $bepInExName) -Force
foreach ($readmeName in $readmeNames) {
    Copy-Item -LiteralPath (Join-Path $projectPath $readmeName) -Destination $releasePath -Force
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archivePath = Join-Path $releasePath $fullArchiveName
$sourceZip = [IO.Compression.ZipFile]::OpenRead($BepInExArchivePath)
try {
    $zipStream = [IO.File]::Open($archivePath, [IO.FileMode]::Create)
    try {
        $destinationZip = [IO.Compression.ZipArchive]::new($zipStream, [IO.Compression.ZipArchiveMode]::Create, $true)
        try {
            # Preserve every official BepInEx file, including the hidden Doorstop version file.
            foreach ($entry in $sourceZip.Entries) {
                $destinationEntry = $destinationZip.CreateEntry($entry.FullName, [IO.Compression.CompressionLevel]::Optimal)
                $sourceStream = $entry.Open()
                try {
                    $destinationStream = $destinationEntry.Open()
                    try { $sourceStream.CopyTo($destinationStream) }
                    finally { $destinationStream.Dispose() }
                } finally { $sourceStream.Dispose() }
            }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($destinationZip,
                (Join-Path $releasePath 'PCBS-Helper.dll'),
                'BepInEx/plugins/PCBS-Helper/PCBS-Helper.dll') | Out-Null
            foreach ($readmeName in $readmeNames) {
                [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($destinationZip,
                    (Join-Path $projectPath $readmeName), $readmeName) | Out-Null
            }
            [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($destinationZip,
                (Join-Path $projectPath 'docs\licenses\BepInEx.txt'), 'BepInEx-LICENSE.txt') | Out-Null
        } finally { $destinationZip.Dispose() }
    } finally { $zipStream.Dispose() }
} finally { $sourceZip.Dispose() }

Write-Output "Release files: $releasePath"
Get-ChildItem -LiteralPath $releasePath -File | Select-Object Name, Length
