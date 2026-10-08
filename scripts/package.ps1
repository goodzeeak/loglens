param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version = '0.1.0')
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$artifactRoot = Join-Path $repoRoot 'artifacts'
$staging = Join-Path $artifactRoot ('stage-' + [guid]::NewGuid().ToString('N'))
$packageRoot = Join-Path $staging ('LogLens-' + $Version + '-win-x64')
New-Item -ItemType Directory -Path $packageRoot -Force | Out-Null
Push-Location $repoRoot
try {
    [xml]$properties = Get-Content -LiteralPath 'Directory.Build.props' -Raw
    if ($properties.Project.PropertyGroup.Version -ne $Version) { throw 'Package version must match Directory.Build.props.' }
    dotnet test LogLens.sln -c Release --logger 'trx;LogFileName=release-gate.trx'
    if ($LASTEXITCODE -ne 0) { throw 'Diagnostic/build/test release gate failed.' }
    dotnet publish src/LogLens.App/LogLens.App.csproj -c Release -r win-x64 --self-contained true -o $packageRoot -p:DebugType=None -p:PublishSingleFile=false
    if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
    Copy-Item -LiteralPath README.md,LICENSE -Destination $packageRoot
    Copy-Item -LiteralPath docs -Destination $packageRoot -Recurse
    Copy-Item -LiteralPath branding -Destination $packageRoot -Recurse
    foreach ($required in @('LogLens.exe','LogLens.dll','LogLens.runtimeconfig.json','coreclr.dll','hostfxr.dll','PresentationFramework.dll','System.Diagnostics.EventLog.dll','README.md','LICENSE')) {
        if (-not (Test-Path -LiteralPath (Join-Path $packageRoot $required))) { throw "Missing portable runtime file: $required" }
    }
    $runtime = Get-Content -LiteralPath (Join-Path $packageRoot 'LogLens.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $runtime.runtimeOptions.includedFrameworks) { throw 'Portable package must include the .NET runtime.' }
    if (-not ($runtime.runtimeOptions.includedFrameworks.name -contains 'Microsoft.WindowsDesktop.App')) { throw 'Missing self-contained Windows desktop framework.' }
    $executableBytes = [IO.File]::ReadAllBytes((Join-Path $packageRoot 'LogLens.exe'))
    $peOffset = [BitConverter]::ToInt32($executableBytes, 0x3c)
    if ([BitConverter]::ToUInt16($executableBytes, $peOffset + 4) -ne 0x8664) { throw 'Portable executable must be x64.' }
    Add-Type -AssemblyName System.Drawing
    $icon = [Drawing.Icon]::ExtractAssociatedIcon((Join-Path $packageRoot 'LogLens.exe'))
    if (-not $icon) { throw 'Executable icon is missing.' }
    $icon.Dispose()
    $zip = Join-Path $artifactRoot ('LogLens-' + $Version + '-win-x64.zip')
    Compress-Archive -LiteralPath $packageRoot -DestinationPath $zip -Force
    $hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content -LiteralPath ($zip + '.sha256') -Value ($hash + '  ' + (Split-Path $zip -Leaf)) -Encoding ascii
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        if ($archive.Entries.Count -lt 30) { throw 'Archive unexpectedly small.' }
        if (-not ($archive.Entries.FullName -match 'LogLens.exe$')) { throw 'Archive has no executable.' }
    } finally { $archive.Dispose() }
    Write-Output "Portable package: $zip"
    Write-Output "SHA-256: $hash"
} finally { Pop-Location }
