# Publish script for FactorioModManager
# Usage: .\Publish.ps1 [-Platform win-x64|linux-x64|osx-x64|osx-arm64] [-Configuration Release|Debug] [-OutputPath ./publish]

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet('win-x64', 'linux-x64', 'osx-x64', 'osx-arm64')]
    [string]$Platform = 'win-x64',
    
    [Parameter(Mandatory=$false)]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = './publish'
)

$ErrorActionPreference = "Stop"

Write-Host "========================================" -ForegroundColor Cyan
Write-Host "Publishing FactorioModManager (all platforms)" -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Cyan
Write-Host "Output Path: $OutputPath" -ForegroundColor Cyan
Write-Host "========================================" -ForegroundColor Cyan
Write-Host ""

# Check if dotnet is installed
try {
    $dotnetVersion = dotnet --version
    Write-Host "Using .NET SDK version: $dotnetVersion" -ForegroundColor Green
} catch {
    Write-Host "Error: .NET SDK is not installed or not in PATH" -ForegroundColor Red
    exit 1
}

# Determine version from csproj (fallback to git tag, then default)
$version = '0.0.0'
$csprojPath = Join-Path -Path (Get-Location) -ChildPath 'UI\FactorioModManager.csproj'
if (Test-Path $csprojPath) {
    try {
        [xml]$xml = Get-Content $csprojPath
        # Look for <Version> in any PropertyGroup
        $verNode = $xml.Project.PropertyGroup | ForEach-Object { $_.Version } | Where-Object { $_ -and $_.ToString().Trim() -ne '' } | Select-Object -First 1
        if ($verNode) {
            $version = $verNode.ToString().Trim()
            Write-Host "Detected app version from csproj: $version" -ForegroundColor Green
        } else {
            # Try AssemblyInformationalVersion
            $infoNode = $xml.Project.PropertyGroup | ForEach-Object { $_.AssemblyInformationalVersion } | Where-Object { $_ -and $_.ToString().Trim() -ne '' } | Select-Object -First 1
            if ($infoNode) {
                $version = $infoNode.ToString().Trim()
                Write-Host "Detected informational version from csproj: $version" -ForegroundColor Green
            } else {
                Write-Host "No <Version> or <AssemblyInformationalVersion> found in csproj, attempting git tag fallback" -ForegroundColor Yellow
                if (Get-Command git -ErrorAction SilentlyContinue) {
                    try {
                        $gitTag = git describe --tags --abbrev=0 2>$null
                        if ($LASTEXITCODE -eq 0 -and $gitTag) {
                            $version = $gitTag.Trim() -replace '^v',''
                            Write-Host "Detected version from git tag: $version" -ForegroundColor Green
                        }
                    } catch {
                        Write-Host "Git describe failed, using default version $version" -ForegroundColor Yellow
                    }
                } else {
                    Write-Host "git not found, using default version $version" -ForegroundColor Yellow
                }
            }
        }
    } catch {
        Write-Host "Warning: Failed to read $csprojPath - $_" -ForegroundColor Yellow
    }
} else {
    Write-Host "Warning: $csprojPath not found, using default version $version" -ForegroundColor Yellow
}

# RIDs to publish for
$rids = @(
    'win-x64',
    'linux-x64',
    'osx-x64',
    'osx-arm64'
)

$projectPath = $csprojPath
if (-not (Test-Path $projectPath)) {
    Write-Host "Project not found at $projectPath" -ForegroundColor Red
    exit 1
}

foreach ($rid in $rids) {
    Write-Host ""
    Write-Host "Publishing for RID: $rid" -ForegroundColor Yellow

    $fullOutputPath = Join-Path $OutputPath $rid
    if (Test-Path $fullOutputPath) {
        Write-Host "Cleaning existing output directory: $fullOutputPath" -ForegroundColor Yellow
        Remove-Item -Path $fullOutputPath -Recurse -Force
    }
    New-Item -ItemType Directory -Path $fullOutputPath | Out-Null

    # Publish the application for this RID
    dotnet publish $projectPath `
        --configuration $Configuration `
        --runtime $rid `
        --self-contained true `
        --output $fullOutputPath `
        /p:PublishSingleFile=true `
        /p:IncludeAllContentForSelfExtract=true

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Publish failed for $rid" -ForegroundColor Red
        exit $LASTEXITCODE
    }

    # Create archive - include version in filename
    $archiveBase = Join-Path $OutputPath "FactorioModManager-$rid-$Configuration-v$version"
    $createdArchivePath = $null

    if ($rid -like 'win-*') {
        $zipPath = "$archiveBase.zip"
        if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
        Write-Host "Creating ZIP archive: $zipPath" -ForegroundColor Yellow
        try {
            Compress-Archive -Path (Join-Path $fullOutputPath '*') -DestinationPath $zipPath -Force -ErrorAction Stop
            $createdArchivePath = $zipPath
        } catch {
            Write-Host "Error: Failed to create ZIP archive for $rid - $_" -ForegroundColor Red
            exit 1
        }
    } else {
        $tarGz = "$archiveBase.tar.gz"
        if (Test-Path $tarGz) { Remove-Item $tarGz -Force }
        Write-Host "Creating tar.gz archive: $tarGz" -ForegroundColor Yellow
        try {
            & tar -C $fullOutputPath -czf $tarGz .
            if ($LASTEXITCODE -ne 0) { throw "tar failed" }
            $createdArchivePath = $tarGz
        } catch {
            # Fallback to 7z if tar not available
            $sevenZip = Get-Command 7z -ErrorAction SilentlyContinue
            if ($sevenZip) {
                $tempTar = "$archiveBase.tar"
                if (Test-Path $tempTar) { Remove-Item $tempTar -Force }
                Write-Host "tar not available or failed, using 7z fallback" -ForegroundColor Yellow
                & 7z a -ttar $tempTar (Join-Path $fullOutputPath '*') | Out-Null
                & 7z a -tgzip $tarGz $tempTar | Out-Null
                Remove-Item $tempTar -Force
                $createdArchivePath = $tarGz
            } else {
                Write-Host "Unable to create tar.gz: neither tar nor 7z available" -ForegroundColor Red
                exit 1
            }
        }
    }

    if ($createdArchivePath -and (Test-Path $createdArchivePath)) {
        Write-Host "Archive created successfully for ${rid}: ${createdArchivePath}" -ForegroundColor Green
    } else {
        Write-Host "Warning: Archive file was not created for ${rid}" -ForegroundColor Yellow
    }
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Publish and packaging completed successfully for all platforms!" -ForegroundColor Green
Write-Host "Artifacts directory: $OutputPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green
