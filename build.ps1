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
        $releaseDocs = Join-Path $outputDocs 'releases'
        New-Item -ItemType Directory -Path $releaseDocs -Force | Out-Null
        Copy-Item -Path docs/releases/*.md -Destination $releaseDocs

        # Carry the licenses from the exact runtime packages restored for this build.
        $packageRootOutput = & $dotnetCommand nuget locals global-packages --list
        if ($LASTEXITCODE -ne 0) { throw 'Could not locate runtime package licenses' }
        $packageRoot = ($packageRootOutput | Select-Object -First 1) -replace '^[^:]+:\s*', ''
        $runtimeConfig = Get-Content (Join-Path $OutputDirectory 'FastWindowResizer.runtimeconfig.json') -Raw | ConvertFrom-Json
        foreach ($framework in $runtimeConfig.runtimeOptions.includedFrameworks) {
            $packageId = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
            $packageDirectory = Join-Path $packageRoot "$packageId/$($framework.version)"
            $noticeDirectory = Join-Path $OutputDirectory "third-party/$($framework.name)"
            New-Item -ItemType Directory -Path $noticeDirectory -Force | Out-Null
            $licenseFiles = Get-ChildItem -LiteralPath $packageDirectory -File | Where-Object { $_.Name -match '^(LICENSE(\.TXT)?|THIRD-PARTY-NOTICES\.TXT)$' }
            if (-not $licenseFiles) { throw "Runtime license missing: $packageId" }
            $licenseFiles | Copy-Item -Destination $noticeDirectory
        }
    }
} finally { Pop-Location }
