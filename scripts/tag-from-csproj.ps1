# Tag the repo using the <Version> from the csproj
param(
    [string]$CsprojPath = "UI\FactorioModManager.csproj"
)

if (-not (Test-Path $CsprojPath)) {
    Write-Error "Csproj not found: $CsprojPath"
    exit 1
}

[xml]$xml = Get-Content $CsprojPath
$versionNode = $xml.Project.PropertyGroup.Version
if ($null -eq $versionNode -or [string]::IsNullOrEmpty($versionNode)) {
    Write-Error "No <Version> element found in $CsprojPath"
    exit 1
}

$version = $versionNode.Trim()
$tag = "v$version"

# Ensure working tree is clean
$status = git status --porcelain
if ($status) {
    Write-Error "Working tree is not clean. Commit or stash changes before tagging."
    exit 1
}

# Create annotated tag if missing
if (git rev-parse --verify $tag >/dev/null 2>&1) {
    Write-Host "Tag $tag already exists."
    exit 0
}

git tag -a $tag -m "Release $tag"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create tag $tag"
    exit 1
}

git push origin $tag
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to push tag $tag to origin"
    exit 1
}

Write-Host "Created and pushed tag $tag"