<#
.SYNOPSIS
    Zips the project keeping only code files and folder structure.

.DESCRIPTION
    Creates a clean zip of the project excluding build artifacts, IDE metadata,
    version control folders, packages, and other non-code noise.

.PARAMETER SourcePath
    Root directory of the project. Defaults to the directory containing this script.

.PARAMETER OutputPath
    Where to save the zip file. Defaults to the Desktop.

.PARAMETER OutputName
    Base name (no extension) for the zip. Defaults to "<FolderName>_code_<timestamp>".

.EXAMPLE
    .\Export-CodeFiles.ps1
    .\Export-CodeFiles.ps1 -SourcePath "D:\MyProject" -OutputPath "C:\Exports"
#>

[CmdletBinding()]
param(
    [string] $SourcePath = $(if ($PSScriptRoot) { $PSScriptRoot } else { Get-Location }),
    [string] $OutputPath = [Environment]::GetFolderPath("Desktop"),
    [string] $OutputName = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# Excluded folder names (matched against every path segment)
$ExcludedFolders = @(
    # .NET build output
    "bin", "obj",
    # IDE / editor
    ".vs", ".idea", ".vscode",
    # Version control
    ".git", ".svn", ".hg",
    # Node / JS
    "node_modules", ".next", ".nuxt", "dist", "build",
    # Python
    "__pycache__", ".venv", "venv", "env",
    # Package caches
    "packages", ".nuget",
    # Misc
    "TestResults", ".terraform", "coverage", ".nyc_output",
    # This tool's own artifacts folder
    "artifacts"
)

# Excluded file extensions
$ExcludedExtensions = @(
    # Compiled / binary
    ".exe", ".dll", ".pdb", ".so", ".dylib", ".lib", ".a", ".o",
    # Archives / packages
    ".zip", ".nupkg", ".snupkg", ".tar", ".gz", ".7z", ".rar",
    # Database / data files
    ".mdf", ".ldf", ".db", ".sqlite", ".bak",
    # Logs
    ".log",
    # OS metadata
    ".DS_Store",
    # Secrets
    ".pfx", ".p12", ".key",
    # Generated / coverage reports
    ".coverage", ".coveragexml", ".trx"
)

# Excluded file names (exact, case-insensitive)
$ExcludedFileNames = @(
    "thumbs.db", ".ds_store", "desktop.ini",
    "package-lock.json", "yarn.lock"
)

# Optional whitelist - uncomment to restrict to specific extensions only:
# $IncludedExtensions = @(".cs",".csproj",".sln",".slnx",".razor",".html",
#                         ".css",".js",".ts",".json",".xml",".yml",".yaml",
#                         ".md",".txt",".ps1",".sh",".sql")

function Test-IsExcludedPath {
    param([System.IO.FileSystemInfo] $Item)
    $rel = $Item.FullName.Substring($SourcePath.TrimEnd('\', '/').Length).TrimStart('\', '/')
    $segments = $rel -split '[/\\]'
    # Check every folder segment (all but the last, which is the filename)
    for ($i = 0; $i -lt ($segments.Length - 1); $i++) {
        if ($ExcludedFolders -contains $segments[$i].ToLower()) { return $true }
    }
    return $false
}

function Test-IsExcludedFile {
    param([System.IO.FileInfo] $File)
    $ext  = $File.Extension.ToLower()
    $name = $File.Name.ToLower()
    if ($ExcludedFileNames -contains $name)  { return $true }
    if ($ExcludedExtensions -contains $ext)  { return $true }
    if ($ExcludedExtensions -contains $name) { return $true }
    # Uncomment for whitelist mode:
    # if ($IncludedExtensions -and ($IncludedExtensions -notcontains $ext)) { return $true }
    return $false
}

# Resolve paths
$SourcePath = (Resolve-Path $SourcePath).Path.TrimEnd('\')

if (-not (Test-Path $OutputPath)) {
    New-Item -ItemType Directory -Path $OutputPath | Out-Null
}
$OutputPath = (Resolve-Path $OutputPath).Path.TrimEnd('\')

if (-not $OutputName) {
    $timestamp  = Get-Date -Format "yyyyMMdd_HHmmss"
    $folderName = Split-Path $SourcePath -Leaf
    $OutputName = "${folderName}_code_${timestamp}"
}

$ZipPath = Join-Path $OutputPath "$OutputName.zip"

Write-Host ""
Write-Host "  Source : $SourcePath"  -ForegroundColor Cyan
Write-Host "  Output : $ZipPath"     -ForegroundColor Cyan
Write-Host ""

# Collect files
Write-Host "  Scanning files..." -ForegroundColor Yellow

$allFiles = Get-ChildItem -Path $SourcePath -Recurse -File

$includedFiles = $allFiles | Where-Object {
    (-not (Test-IsExcludedPath $_)) -and (-not (Test-IsExcludedFile $_))
}

Write-Host ("  Found  : " + $allFiles.Count + " total files")     -ForegroundColor Gray
Write-Host ("  Keeping: " + $includedFiles.Count + " code files") -ForegroundColor Green
Write-Host ""

if ($includedFiles.Count -eq 0) {
    Write-Warning "No files matched. Check your ExcludedFolders / ExcludedExtensions lists."
    exit 1
}

# Build zip via a temp staging folder
$tempDir = Join-Path $env:TEMP ("code_export_" + (Get-Random))
New-Item -ItemType Directory -Path $tempDir | Out-Null

try {
    foreach ($file in $includedFiles) {
        $relativePath = $file.FullName.Substring($SourcePath.Length).TrimStart('\', '/')
        $destFile     = Join-Path $tempDir $relativePath
        $destDir      = Split-Path $destFile -Parent

        if (-not (Test-Path $destDir)) {
            New-Item -ItemType Directory -Path $destDir | Out-Null
        }
        Copy-Item -Path $file.FullName -Destination $destFile
    }

    Write-Host "  Creating zip..." -ForegroundColor Yellow

    if (Test-Path $ZipPath) { Remove-Item $ZipPath -Force }

    Add-Type -AssemblyName System.IO.Compression.FileSystem
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempDir, $ZipPath)

    $sizeBytes = (Get-Item $ZipPath).Length
    $sizeMB    = [math]::Round($sizeBytes / 1048576, 2)

    Write-Host ""
    Write-Host "  Done! Zip saved to:" -ForegroundColor Green
    Write-Host ("    " + $ZipPath + "  (" + $sizeMB + " MB)") -ForegroundColor White
    Write-Host ""
}
finally {
    Remove-Item -Recurse -Force $tempDir -ErrorAction SilentlyContinue
}
