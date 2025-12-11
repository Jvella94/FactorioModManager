# Build and Publish Scripts

This directory contains scripts to build and publish the FactorioModManager application for local development and distribution.

## Available Scripts

### Build Scripts

#### Windows (PowerShell)
```powershell
.\Build.ps1 [-Configuration Release|Debug]
```

**Examples:**
```powershell
# Build in Release mode (default)
.\Build.ps1

# Build in Debug mode
.\Build.ps1 -Configuration Debug
```

#### Linux/macOS (Bash)
```bash
./build.sh [Debug|Release]
```

**Examples:**
```bash
# Build in Release mode (default)
./build.sh

# Build in Debug mode
./build.sh Debug
```

### Publish Scripts

#### Windows (PowerShell)
```powershell
.\Publish.ps1 [-Platform win-x64|linux-x64|osx-x64|osx-arm64] [-Configuration Release|Debug] [-OutputPath ./publish]
```

**Examples:**
```powershell
# Publish for Windows (default)
.\Publish.ps1

# Publish for Linux
.\Publish.ps1 -Platform linux-x64

# Publish for macOS Intel
.\Publish.ps1 -Platform osx-x64

# Publish for macOS Apple Silicon
.\Publish.ps1 -Platform osx-arm64

# Publish for Windows in Debug mode to custom path
.\Publish.ps1 -Platform win-x64 -Configuration Debug -OutputPath ./output
```

#### Linux/macOS (Bash)
```bash
./publish.sh [Platform] [Configuration] [OutputPath]
```

**Examples:**
```bash
# Publish for Linux (default)
./publish.sh

# Publish for Windows from Linux/macOS
./publish.sh win-x64

# Publish for macOS Intel
./publish.sh osx-x64

# Publish for macOS Apple Silicon
./publish.sh osx-arm64

# Publish in Debug mode to custom path
./publish.sh linux-x64 Debug ./output
```

## Using Scripts in Visual Studio

### Option 1: Using Task Runner Explorer

1. Open Visual Studio
2. Go to `View` → `Other Windows` → `Task Runner Explorer`
3. Right-click on the project and add these scripts as external tools

### Option 2: Using External Tools

1. In Visual Studio, go to `Tools` → `External Tools...`
2. Click `Add` and configure:
   - **Build Script:**
     - Title: `Build Release`
     - Command: `powershell.exe`
     - Arguments: `-ExecutionPolicy Bypass -File "$(ProjectDir)Build.ps1" -Configuration Release`
     - Initial directory: `$(ProjectDir)`
     - Check "Use Output window"
   
   - **Publish Script:**
     - Title: `Publish Windows x64`
     - Command: `powershell.exe`
     - Arguments: `-ExecutionPolicy Bypass -File "$(ProjectDir)Publish.ps1" -Platform win-x64 -Configuration Release`
     - Initial directory: `$(ProjectDir)`
     - Check "Use Output window"

### Option 3: Using PowerShell Directly in Visual Studio

1. Open the Package Manager Console in Visual Studio (`View` → `Other Windows` → `Package Manager Console`)
2. Run scripts directly:
   ```powershell
   .\Build.ps1
   .\Publish.ps1 -Platform win-x64
   ```

### Option 4: Creating Custom Build Events

1. Right-click on the project → `Properties`
2. Go to `Build Events`
3. Add to `Pre-build event command line` or `Post-build event command line`:
   ```
   powershell.exe -ExecutionPolicy Bypass -File "$(ProjectDir)Build.ps1"
   ```

## Script Features

### Build Script (`Build.ps1` / `build.sh`)
- Validates .NET SDK installation
- Restores NuGet dependencies
- Builds the solution in specified configuration
- Provides colored output and error handling
- Shows build output location

### Publish Script (`Publish.ps1` / `publish.sh`)
- Validates .NET SDK installation
- Creates self-contained executables for specified platform
- Includes .NET runtime (no installation required on target machine)
- Cleans output directory before publishing
- Optional archive creation (ZIP for Windows, TAR.GZ for Unix)
- Supports all major platforms:
  - Windows x64
  - Linux x64
  - macOS Intel (x64)
  - macOS Apple Silicon (ARM64)

## Output Locations

- **Build output:** `UI/bin/{Configuration}/net9.0/`
- **Publish output:** `{OutputPath}/{Platform}/`
- **Archives:** `{OutputPath}/FactorioModManager-{Platform}.{zip|tar.gz}`

## Requirements

- .NET 9.0 SDK or later
- PowerShell 5.1+ (Windows) or PowerShell Core 7+ (cross-platform)
- Bash (for Linux/macOS scripts)

## Troubleshooting

### PowerShell Execution Policy Error
If you get an execution policy error, run PowerShell as Administrator and execute:
```powershell
Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
```

Or run scripts with bypass:
```powershell
powershell.exe -ExecutionPolicy Bypass -File .\Build.ps1
```

### Permission Denied (Linux/macOS)
Make the bash scripts executable:
```bash
chmod +x build.sh publish.sh
```

### .NET SDK Not Found
Ensure .NET 9.0 SDK is installed:
```bash
dotnet --version
```

Download from: https://dotnet.microsoft.com/download

## See Also

- [GitHub Actions Release Workflow](.github/workflows/README.md) - Automated CI/CD releases
- [Project README](README.md) - Main project documentation
