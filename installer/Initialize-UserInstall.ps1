[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallRoot,
    [Parameter(Mandatory)][string]$UserSid,
    [Parameter(Mandatory)][string]$BackupDirectory
)

$ErrorActionPreference = 'Stop'
$identity = [Security.Principal.WindowsIdentity]::GetCurrent()
if (-not [Security.Principal.WindowsPrincipal]::new($identity).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    throw 'Initial migration requires a Windows administrator.'
}
$user = [Security.Principal.SecurityIdentifier]::new($UserSid)
if ($user.Value -notlike 'S-1-5-21-*') { throw 'Specify the SID of the intended Windows user.' }
[void]$user.Translate([Security.Principal.NTAccount])

$root = [IO.Path]::GetFullPath($InstallRoot).TrimEnd('\')
$backup = [IO.Path]::GetFullPath($BackupDirectory).TrimEnd('\')
if ($root -eq [IO.Path]::GetPathRoot($root).TrimEnd('\') -or $backup.StartsWith($root + '\', [StringComparison]::OrdinalIgnoreCase) -or
    $backup -eq $root -or (Test-Path -LiteralPath $backup)) {
    throw 'The backup must be a new directory outside the installation root.'
}
$legacy = @(Get-ItemProperty 'HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\*' -ErrorAction SilentlyContinue |
    Where-Object { $_.DisplayName -eq 'Hataori' -and $_.Publisher -eq 'Akatsukisoft' -and $_.WindowsInstaller -eq 1 })
if ($legacy.Count -ne 1 -or $legacy[0].PSChildName -notmatch '^\{[a-fA-F0-9-]{36}\}$') {
    throw 'Expected exactly one legacy machine-wide Hataori MSI.'
}
$expectedExecutable = Join-Path $root 'bin\server\Hataori.Server.exe'
$servicePath = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\Hataori').ImagePath.Trim('"')
if (-not $servicePath.Equals($expectedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The Windows service is not registered at the selected installation root.'
}
if ((Get-Item -LiteralPath $root).Attributes -band [IO.FileAttributes]::ReparsePoint -or
    (Get-ChildItem -LiteralPath $root -Recurse -Force -Attributes ReparsePoint)) {
    throw 'The installation contains a reparse point.'
}
$configPath = Join-Path $root 'config\hataori.json'
$config = Get-Content -LiteralPath $configPath -Raw | ConvertFrom-Json
$databaseSetting = [string]$config.server.databasePath
$database = if ([IO.Path]::IsPathRooted($databaseSetting)) {
    [IO.Path]::GetFullPath($databaseSetting)
} else {
    [IO.Path]::GetFullPath((Join-Path $root $databaseSetting))
}
$dataDirectory = (Join-Path $root 'data') + '\'
if (-not $database.StartsWith($dataDirectory, [StringComparison]::OrdinalIgnoreCase) -or -not (Test-Path -LiteralPath $database -PathType Leaf)) {
    throw 'Inspect the database location manually before migrating a nonstandard installation.'
}
$legacySecret = Join-Path $root 'config\hataori.service.json'
$userSecret = Join-Path $root 'config\hataori.user.json'
if ((Test-Path -LiteralPath $legacySecret) -and (Test-Path -LiteralPath $userSecret)) {
    throw 'Both service and user credential files exist; resolve the conflict before migrating.'
}

function Set-Access([string]$path, [string]$sddl) {
    $acl = Get-Acl -LiteralPath $path
    $acl.SetSecurityDescriptorSddlForm($sddl, [Security.AccessControl.AccessControlSections]::Access)
    Set-Acl -LiteralPath $path -AclObject $acl
}

New-Item -ItemType Directory -Path $backup | Out-Null
Set-Access $backup 'D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)'
$resultPath = Join-Path $backup 'migration.txt'
function Write-Step([string]$message) { Add-Content -LiteralPath $resultPath -Value $message }

try {
    $monitorExecutable = Join-Path $root 'bin\monitor\Hataori.Monitor.exe'
    foreach ($monitor in @(Get-Process Hataori.Monitor -ErrorAction SilentlyContinue)) {
        if ($monitor.Path -eq $monitorExecutable) {
            Stop-Process -Id $monitor.Id
            if (-not $monitor.WaitForExit(10000)) { throw 'The legacy Monitor did not exit.' }
        }
    }
    Stop-Service Hataori
    (Get-Service Hataori).WaitForStatus('Stopped', [TimeSpan]::FromSeconds(30))
    foreach ($name in @('config', 'data')) {
        $from = Join-Path $root $name
        $to = Join-Path $backup $name
        & "$env:SystemRoot\System32\robocopy.exe" $from $to /E /COPY:DAT /DCOPY:DAT /XJ /R:1 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null
        if ($LASTEXITCODE -ge 8) { throw "Backup failed for $name." }
        $files = @(Get-ChildItem -LiteralPath $from -Recurse -Force -File)
        foreach ($file in $files) {
            $relative = $file.FullName.Substring($from.Length + 1)
            if ((Get-FileHash -LiteralPath $file.FullName).Hash -ne (Get-FileHash -LiteralPath (Join-Path $to $relative)).Hash) {
                throw "Backup hash mismatch in $name."
            }
        }
        Write-Step "Verified backup: $name ($($files.Count) files)."
    }
    $oldAcl = (Get-Acl -LiteralPath $root).GetSecurityDescriptorSddlForm([Security.AccessControl.AccessControlSections]::Access)
    Set-Content -LiteralPath (Join-Path $backup 'previous-root-acl.txt') -Value $oldAcl
    $log = Join-Path $backup 'legacy-uninstall.log'
    $uninstall = Start-Process -FilePath "$env:SystemRoot\System32\msiexec.exe" -ArgumentList ('/x {0} /qn /norestart /L*v "{1}"' -f $legacy[0].PSChildName, $log) -Wait -PassThru -WindowStyle Hidden
    Write-Step "Legacy MSI uninstall exit: $($uninstall.ExitCode)."
    if ($uninstall.ExitCode -notin @(0, 3010) -or (Get-Service Hataori -ErrorAction SilentlyContinue)) {
        throw 'The old MSI and service must be removed before granting file access.'
    }
    Set-Access $root "D:P(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)(A;OICI;0x1301bf;;;$UserSid)"
    if (Test-Path -LiteralPath $legacySecret) {
        # Preserve the service-only copy for rollback and grant the user access only to the new copy.
        Copy-Item -LiteralPath $legacySecret -Destination $userSecret
        Set-Access $userSecret "D:P(A;;FA;;;SY)(A;;FA;;;BA)(A;;0x1301bf;;;$UserSid)"
        if ((Get-FileHash -LiteralPath $legacySecret).Hash -ne (Get-FileHash -LiteralPath $userSecret).Hash) {
            throw 'Credential migration hash mismatch.'
        }
        Write-Step 'User credential copy verified.'
    }
    Write-Step 'READY: Legacy service removed. Install the user MSI from the normal user session.'
    exit 0
}
catch {
    Write-Step ('FAILED: ' + $_.Exception.Message)
    if (Get-Service Hataori -ErrorAction SilentlyContinue) {
        try { Start-Service Hataori; Write-Step 'Legacy service restarted.' }
        catch { Write-Step ('Legacy service restart failed: ' + $_.Exception.Message) }
    }
    exit 1
}
