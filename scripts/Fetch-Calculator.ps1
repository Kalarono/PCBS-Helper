$ErrorActionPreference = 'Stop'
$assetPath = Join-Path (Split-Path -Parent $PSScriptRoot) 'assets'
New-Item -ItemType Directory -Path $assetPath -Force | Out-Null
$sourceUrl = 'https://jacobwklein.github.io/PC-Building-Simulator/HTML-Calculator/Current-Version/PC-Building-Simulator-HTML-Calculator.html'
$htmlPath = Join-Path $assetPath 'calculator.html'
Invoke-WebRequest -Uri $sourceUrl -OutFile $htmlPath
@{ url = $sourceUrl; sha256 = (Get-FileHash -LiteralPath $htmlPath).Hash; downloadedAt = [DateTime]::UtcNow.ToString('o') } |
    ConvertTo-Json | Set-Content -LiteralPath (Join-Path $assetPath 'calculator-source.json') -Encoding UTF8
Write-Output 'Downloaded the original Jacob W. Klein calculator; it is kept outside Git.'
