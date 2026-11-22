$ErrorActionPreference = 'Stop'

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The Windows .NET Framework C# compiler was not found.'
}

$pluginSource = Join-Path $PSScriptRoot 'plugin'
$manifestPath = Join-Path $pluginSource 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$dist = Join-Path $PSScriptRoot 'dist'
$package = Join-Path $dist 'local.fifine.micmute.sdPlugin'
New-Item -ItemType Directory -Path $package -Force | Out-Null

$executable = Join-Path $package $manifest.CodePath
$sources = Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot 'src') -File -Filter '*.cs'
& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /r:System.Web.Extensions.dll "/out:$executable" $sources.FullName
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath $manifestPath -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $pluginSource 'images') -Destination $package -Recurse -Force

$archive = Join-Path $dist "fifine-mic-mute-$($manifest.Version).zip"
Compress-Archive -LiteralPath $package -DestinationPath $archive -Force
Write-Output $archive
