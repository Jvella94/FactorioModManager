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
Write-Host "Publishing FactorioModManager" -ForegroundColor Cyan
Write-Host "Platform: $Platform" -ForegroundColor Cyan
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

# Create output directory if it doesn't exist
$fullOutputPath = Join-Path $OutputPath $Platform
if (Test-Path $fullOutputPath) {
    Write-Host "Cleaning existing output directory: $fullOutputPath" -ForegroundColor Yellow
    Remove-Item -Path $fullOutputPath -Recurse -Force
}

# Publish the application
Write-Host ""
Write-Host "Publishing application..." -ForegroundColor Yellow
dotnet publish UI/FactorioModManager.csproj `
    --configuration $Configuration `
    --runtime $Platform `
    --self-contained true `
    --output $fullOutputPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed" -ForegroundColor Red
    exit $LASTEXITCODE
}

Write-Host ""
Write-Host "========================================" -ForegroundColor Green
Write-Host "Publish completed successfully!" -ForegroundColor Green
Write-Host "Platform: $Platform" -ForegroundColor Green
Write-Host "Configuration: $Configuration" -ForegroundColor Green
Write-Host "Output directory: $fullOutputPath" -ForegroundColor Green
Write-Host "========================================" -ForegroundColor Green

# Optionally create an archive
Write-Host ""
$createArchive = Read-Host "Create archive? (y/n) [default: n]"
if ($createArchive -eq 'y' -or $createArchive -eq 'Y') {
    $archiveName = "FactorioModManager-$Platform"
    
    if ($Platform -eq 'win-x64') {
        $archivePath = Join-Path $OutputPath "$archiveName.zip"
        Write-Host "Creating ZIP archive: $archivePath" -ForegroundColor Yellow
        Compress-Archive -Path "$fullOutputPath/*" -DestinationPath $archivePath -Force
    } else {
        $archivePath = Join-Path $OutputPath "$archiveName.tar.gz"
        Write-Host "Creating TAR.GZ archive: $archivePath" -ForegroundColor Yellow
        # Use tar if available on Windows (Windows 10 1803+ has built-in tar)
        tar -czf $archivePath -C $fullOutputPath .
        if ($LASTEXITCODE -ne 0) {
            Write-Host "Warning: tar command failed. Archive not created." -ForegroundColor Yellow
        }
    }
    
    if (Test-Path $archivePath) {
        Write-Host "Archive created successfully: $archivePath" -ForegroundColor Green
    }
}
