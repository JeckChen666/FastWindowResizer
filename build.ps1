param(
    [switch]$Test,
    [string]$OutputDirectory = 'dist/FastWindowResizer'
)
$ErrorActionPreference = 'Stop'
$localSdk = Join-Path $env:LOCALAPPDATA 'FastWindowResizer\dotnet\dotnet.exe'
$dotnetCommand = if (Test-Path -LiteralPath $localSdk) { $localSdk } else { 'dotnet' }
Push-Location $PSScriptRoot
try {
    if ($Test) {
        & $dotnetCommand run --project tests/FastWindowResizer.Checks.csproj -c Release
    } else {
        & $dotnetCommand publish FastWindowResizer.csproj -c Release -r win-x64 --self-contained true -o $OutputDirectory
    }
    if ($LASTEXITCODE -ne 0) { throw "dotnet failed with exit code $LASTEXITCODE" }
    if (-not $Test) {
        Copy-Item -LiteralPath README.md -Destination $OutputDirectory
        $outputDocs = Join-Path $OutputDirectory 'docs'
        New-Item -ItemType Directory -Path $outputDocs -Force | Out-Null
        Copy-Item -LiteralPath docs/PLAN.md,docs/VALIDATION.md -Destination $outputDocs
    }
} finally { Pop-Location }
