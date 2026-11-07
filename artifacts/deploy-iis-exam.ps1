$ErrorActionPreference = 'Stop'
$src = 'D:\New folder (2)\smartpos\artifacts\iis-exam'
$dest = 'C:\inetpub\wwwroot\exam'
$log = 'D:\New folder (2)\smartpos\artifacts\iis-deploy.log'

function Write-Log($message) {
    $line = '{0:u} {1}' -f (Get-Date), $message
    Add-Content -Path $log -Value $line
    Write-Host $line
}

try {
    Write-Log 'Starting IIS deploy'
    if (-not ([Security.Principal.WindowsPrincipal][Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
        throw 'This script must run elevated. Right-click the bat and choose Run as administrator.'
    }

    New-Item -ItemType Directory -Path $dest -Force | Out-Null
    & robocopy $src $dest /E /NFL /NDL /NJH /NJS /nc /ns /np | Out-Null
    if ($LASTEXITCODE -ge 8) { throw "robocopy failed with exit $LASTEXITCODE" }
    Write-Log 'Copied published files'

    New-Item -ItemType Directory -Path (Join-Path $dest 'logs') -Force | Out-Null
    icacls (Join-Path $dest 'logs') /grant 'IIS APPPOOL\exam:(OI)(CI)M' | Out-Null
    Write-Log 'Granted app pool modify on logs'

    Import-Module WebAdministration
    if (Test-Path 'IIS:\AppPools\exam') {
        Restart-WebAppPool -Name 'exam'
        Write-Log 'Recycled app pool exam'
    } else {
        Write-Log 'App pool exam not found; run iisreset if the site does not pick up the new files'
    }

    Write-Log 'Deploy succeeded'
    exit 0
}
catch {
    Write-Log ("Deploy failed: " + $_.Exception.Message)
    exit 1
}
