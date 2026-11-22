param(
    [ValidateSet('Microphone', 'Unity')]
    [string]$Plugin = 'Microphone'
)

$ErrorActionPreference = 'Stop'

$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path -LiteralPath $compiler -PathType Leaf)) {
    throw 'The Windows .NET Framework C# compiler was not found.'
}

$sourceRoot = $PSScriptRoot
$pluginId = 'local.fifine.micmute'
$archiveName = 'fifine-mic-mute'
if ($Plugin -eq 'Unity') {
    $sourceRoot = Join-Path $PSScriptRoot 'unity'
    $pluginId = 'local.fifine.unitycontrols'
    $archiveName = 'fifine-unity-controls'
}

$pluginSource = Join-Path $sourceRoot 'plugin'
$manifestPath = Join-Path $pluginSource 'manifest.json'
$manifest = Get-Content -LiteralPath $manifestPath -Encoding UTF8 -Raw | ConvertFrom-Json
$dist = Join-Path $PSScriptRoot 'dist'
$package = Join-Path $dist "$pluginId.sdPlugin"
New-Item -ItemType Directory -Path $package -Force | Out-Null

$executable = Join-Path $package $manifest.CodePath
$sources = @(Get-ChildItem -LiteralPath (Join-Path $sourceRoot 'src') -File -Filter '*.cs' | Select-Object -ExpandProperty FullName)
$archiveContents = @($package)
if ($Plugin -eq 'Unity') {
    $editorPackage = Join-Path $sourceRoot 'package'
    $sources += Join-Path $PSScriptRoot 'src\StreamDockConnection.cs'
    $sources += Join-Path $editorPackage 'Editor\UnityCommandProtocol.cs'
    $archiveContents += $editorPackage
    $archiveContents += Join-Path $sourceRoot 'README.md'
}

& $compiler /nologo /target:winexe /platform:anycpu /optimize+ /warnaserror+ /r:System.Web.Extensions.dll "/out:$executable" $sources
if ($LASTEXITCODE -ne 0) {
    throw "C# compilation failed with exit code $LASTEXITCODE."
}

Copy-Item -LiteralPath $manifestPath -Destination $package -Force
Copy-Item -LiteralPath (Join-Path $pluginSource 'images') -Destination $package -Recurse -Force

$archive = Join-Path $dist "$archiveName-$($manifest.Version).zip"
Compress-Archive -LiteralPath $archiveContents -DestinationPath $archive -Force
Write-Output $archive
