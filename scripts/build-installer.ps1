param(
    [string]$PublishDirectory = 'dist/FastWindowResizer',
    [string]$OutputDirectory = 'dist/installer',
    [string]$CompilerPath
)
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
Push-Location $projectDirectory
try {
    if (-not $CompilerPath) {
        $candidates = @(
            (Join-Path $env:LOCALAPPDATA 'FastWindowResizer\tools\InnoSetup6\ISCC.exe'),
            (Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'),
            (Join-Path $env:ProgramFiles 'Inno Setup 7\ISCC.exe')
        )
        $CompilerPath = $candidates | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
        if (-not $CompilerPath) { $CompilerPath = (Get-Command ISCC.exe -ErrorAction SilentlyContinue).Source }
    }
    if (-not $CompilerPath) { throw 'Install Inno Setup 6.7+ or pass -CompilerPath pointing to ISCC.exe.' }
    $publishPath = (Resolve-Path -LiteralPath $PublishDirectory).Path
    $binary = Get-Item -LiteralPath (Join-Path $publishPath 'FastWindowResizer.exe')
    $version = $binary.VersionInfo.FileVersion -replace '\.0$', ''
    if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Invalid application file version' }
    $runtimeConfig = Get-Content (Join-Path $publishPath 'FastWindowResizer.runtimeconfig.json') -Raw | ConvertFrom-Json
    if (-not $runtimeConfig.runtimeOptions.includedFrameworks) { throw 'Installer requires a self-contained publish folder. Run build.ps1 first.' }
    New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
    $outputPath = (Resolve-Path -LiteralPath $OutputDirectory).Path
    & $CompilerPath /Qp "/DAppVersion=$version" "/DPublishDir=$publishPath" "/DInstallerOutputDir=$outputPath" installer/FastWindowResizer.iss
    if ($LASTEXITCODE -ne 0) { throw "Installer compilation failed ($LASTEXITCODE)." }
    Get-Item -LiteralPath (Join-Path $outputPath "FastWindowResizer-v$version-win-x64-setup.exe")
} finally { Pop-Location }
