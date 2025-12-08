# Get directory contents excluding obj, debug, and dot folders
param(
    [Parameter(Mandatory=$false)]
    [string]$Path = (Get-Location).Path,
    
    [Parameter(Mandatory=$false)]
    [switch]$Recurse
)

function Get-FilteredChildItems {
    param(
        [string]$CurrentPath,
        [bool]$IsRecursive
    )
    
    # Get all items in current directory
    $items = Get-ChildItem -Path $CurrentPath -Force
    
    foreach ($item in $items) {
        # Skip hidden folders (starting with .)
        if ($item.Name -like ".*") {
            continue
        }
        
        # Skip obj and debug folders (case-insensitive)
        if ($item.PSIsContainer -and ($item.Name -match "^(obj|debug)$")) {
            continue
        }
        
        # Output the item
        $item
        
        # Recurse into directories if requested
        if ($IsRecursive -and $item.PSIsContainer) {
            Get-FilteredChildItems -CurrentPath $item.FullName -IsRecursive $true
        }
    }
}

# Run the function
Get-FilteredChildItems -CurrentPath $Path -IsRecursive $Recurse
