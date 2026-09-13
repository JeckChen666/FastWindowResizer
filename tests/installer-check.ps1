param([string]$Installer = 'dist/releases/v1.0.0/FastWindowResizer-v1.0.0-win-x64-setup.exe')
$ErrorActionPreference = 'Stop'
$projectDirectory = Split-Path $PSScriptRoot -Parent
Push-Location $projectDirectory
$uninstallKey = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{FA2A415F-A0A4-41C7-B591-5C841BC61451}_is1'
$runKeyPath = 'Software\Microsoft\Windows\CurrentVersion\Run'
$runValueName = 'FastWindowResizer'
$resumePath = $null
$testExecutable = $null
$runKey = $null
$passed = 0
function Assert-Check([bool]$Condition, [string]$Label) {
    if (-not $Condition) { throw "FAIL: $Label" }
    $script:passed++
    Write-Output "PASS: $Label"
}
function Invoke-Setup([string]$Executable, [string[]]$SetupArguments) {
    $process = Start-Process -FilePath $Executable -ArgumentList $SetupArguments -PassThru -WindowStyle Hidden
    if (-not $process.WaitForExit(60000)) { throw 'Setup did not finish within 60 seconds' }
    if ($process.ExitCode -ne 0) { throw "Setup failed with code $($process.ExitCode)" }
}
try {
    if (Test-Path -LiteralPath $uninstallKey) { throw 'An installed copy already exists. Run this check on a clean test account.' }
    $installerPath = (Resolve-Path -LiteralPath $Installer).Path
    $caseId = [guid]::NewGuid().ToString('N')
    $checkRoot = Join-Path $projectDirectory "output\installer-check\$caseId"
    New-Item -ItemType Directory -Path $checkRoot -Force | Out-Null
    $installDirectory = Join-Path $checkRoot 'Installed App'
    $testExecutable = Join-Path $installDirectory 'FastWindowResizer.exe'
    $group = 'FastWindowResizer'
    $shortcut = Join-Path ([Environment]::GetFolderPath('Programs')) "$group\FastWindowResizer.lnk"
    if (Test-Path -LiteralPath $shortcut) { throw 'A Start menu shortcut already exists. Use a clean test account.' }
    $runKey = [Microsoft.Win32.Registry]::CurrentUser.CreateSubKey($runKeyPath)
    $hadRunValue = $runKey.GetValueNames() -contains $runValueName
    if ($hadRunValue) {
        $savedRunValue = $runKey.GetValue($runValueName, $null, [Microsoft.Win32.RegistryValueOptions]::DoNotExpandEnvironmentNames)
        $savedRunKind = $runKey.GetValueKind($runValueName)
    }

    $localPortable = Join-Path $projectDirectory 'dist\FastWindowResizer\FastWindowResizer.exe'
    foreach ($running in @(Get-Process -Name FastWindowResizer -ErrorAction SilentlyContinue)) {
        if ($running.Path -ne $localPortable) { throw 'A different copy is running; close it before this test.' }
        $resumePath = $running.Path
        Stop-Process -Id $running.Id
    }
    $commonArguments = @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART','/LANG=chinesesimplified',('/DIR="' + $installDirectory + '"'),('/GROUP="' + $group + '"'),'/TASKS=""')
    Invoke-Setup $installerPath ($commonArguments + ('/LOG="' + (Join-Path $checkRoot 'install.log') + '"'))
    Assert-Check (Test-Path -LiteralPath $testExecutable) 'self-contained application installed to path with spaces'
    Assert-Check (Test-Path -LiteralPath $uninstallKey) 'uninstall entry registered for current user'
    Assert-Check ((Get-ItemProperty -LiteralPath $uninstallKey).DisplayVersion -eq '1.0.0') 'installed version recorded'
    Assert-Check (Test-Path -LiteralPath $shortcut) 'Start menu shortcut created'
    $shell = New-Object -ComObject WScript.Shell
    $link = $shell.CreateShortcut($shortcut)
    Assert-Check ($link.TargetPath -eq $testExecutable) 'shortcut points to installed executable'
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($link) | Out-Null
    [Runtime.InteropServices.Marshal]::FinalReleaseComObject($shell) | Out-Null
    $app = Start-Process -FilePath $testExecutable -PassThru -WindowStyle Hidden
    Start-Sleep -Seconds 2
    Assert-Check (-not $app.HasExited) 'installed application starts without separate runtime installation'
    Stop-Process -Id $app.Id
    Invoke-Setup $installerPath ($commonArguments + ('/LOG="' + (Join-Path $checkRoot 'reinstall.log') + '"'))
    Assert-Check (Test-Path -LiteralPath $testExecutable) 'reinstall succeeds at same location'

    $runKey.SetValue($runValueName, '"' + $testExecutable + '"', [Microsoft.Win32.RegistryValueKind]::String)
    $uninstaller = (Get-ItemProperty -LiteralPath $uninstallKey).UninstallString.Trim('"')
    Invoke-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART',('/LOG="' + (Join-Path $checkRoot 'uninstall.log') + '"'))
    Assert-Check (-not (Test-Path -LiteralPath $testExecutable)) 'uninstall removes installed application'
    Assert-Check (-not (Test-Path -LiteralPath $uninstallKey)) 'uninstall removes registration'
    Assert-Check (-not (Test-Path -LiteralPath $shortcut)) 'uninstall removes shortcut'
    Assert-Check ($null -eq $runKey.GetValue($runValueName)) 'uninstall removes autostart for installed copy'

    Invoke-Setup $installerPath ($commonArguments + ('/LOG="' + (Join-Path $checkRoot 'install-preserve.log') + '"'))
    $uninstaller = (Get-ItemProperty -LiteralPath $uninstallKey).UninstallString.Trim('"')
    $differentStartup = '"C:\Other Portable Copy\FastWindowResizer.exe"'
    $runKey.SetValue($runValueName, $differentStartup, [Microsoft.Win32.RegistryValueKind]::String)
    Invoke-Setup $uninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
    Assert-Check ($runKey.GetValue($runValueName) -eq $differentStartup) 'uninstall preserves a different portable copy autostart'
    Write-Output "PASS: $passed installer checks. Logs: $checkRoot"
} finally {
    if ($testExecutable) {
        Get-Process -Name FastWindowResizer -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $testExecutable } | Stop-Process
        if (Test-Path -LiteralPath $uninstallKey) {
            $cleanupRegistration = Get-ItemProperty -LiteralPath $uninstallKey
            if ($cleanupRegistration.InstallLocation.TrimEnd('\') -eq (Split-Path $testExecutable -Parent)) {
                $cleanupUninstaller = $cleanupRegistration.UninstallString.Trim('"')
                if (Test-Path -LiteralPath $cleanupUninstaller) {
                    Invoke-Setup $cleanupUninstaller @('/VERYSILENT','/SUPPRESSMSGBOXES','/NORESTART')
                }
            }
        }
    }
    if ($runKey) {
        if ($hadRunValue) { $runKey.SetValue($runValueName, $savedRunValue, $savedRunKind) }
        else { $runKey.DeleteValue($runValueName, $false) }
        $runKey.Dispose()
    }
    if ($resumePath) { Start-Process -FilePath $resumePath -WindowStyle Hidden }
    Pop-Location
}
