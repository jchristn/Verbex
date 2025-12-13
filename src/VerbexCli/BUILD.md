# VerbexCli Build Instructions

This document explains how to build VerbexCli for multiple platforms and architectures.

## Supported Platforms

VerbexCli supports the following platforms and architectures:

- **Windows**: x64, ARM64
- **macOS**: x64 (Intel), ARM64 (Apple Silicon)
- **Linux**: x64, ARM64

## Build Scripts

### Build All Platforms

Build for all supported platforms in one command:

**Windows (PowerShell):**
```powershell
.\build-all.ps1
```

**macOS/Linux (Bash):**
```bash
./build-all.sh
```

Optional parameters:
- Configuration: `Release` (default) or `Debug`
- Output directory: `artifacts` (default)

**Examples:**
```powershell
# PowerShell
.\build-all.ps1 Release artifacts
.\build-all.ps1 Debug build-output
```

```bash
# Bash
./build-all.sh Release artifacts
./build-all.sh Debug build-output
```

### Platform-Specific Builds

Build for specific platforms only:

**Windows Only:**
```powershell
.\build-windows.ps1
```

**macOS Only:**
```bash
./build-macos.sh
```

**Linux Only:**
```bash
./build-linux.sh
```

### Manual Build

You can also build manually using `dotnet` commands:

```bash
# Windows x64
dotnet publish -c Release -r win-x64 --self-contained -o artifacts/win-x64

# Windows ARM64
dotnet publish -c Release -r win-arm64 --self-contained -o artifacts/win-arm64

# macOS x64 (Intel)
dotnet publish -c Release -r osx-x64 --self-contained -o artifacts/osx-x64

# macOS ARM64 (Apple Silicon)
dotnet publish -c Release -r osx-arm64 --self-contained -o artifacts/osx-arm64

# Linux x64
dotnet publish -c Release -r linux-x64 --self-contained -o artifacts/linux-x64

# Linux ARM64
dotnet publish -c Release -r linux-arm64 --self-contained -o artifacts/linux-arm64
```

## Output

After building, the executables will be located in the `artifacts` directory (or your specified output directory):

```
artifacts/
├── win-x64/
│   └── vbx.exe
├── win-arm64/
│   └── vbx.exe
├── osx-x64/
│   └── vbx
├── osx-arm64/
│   └── vbx
├── osx-universal/      # macOS only: universal binary combining x64 and ARM64
│   └── vbx
├── linux-x64/
│   └── vbx
└── linux-arm64/
    └── vbx
```

## Build Features

All builds include:

- **Single-file deployment**: All dependencies packaged into a single executable
- **Self-contained**: No .NET runtime installation required
- **ReadyToRun compilation**: Faster startup times
- **Compression**: Smaller executable size
- **Embedded debug symbols**: For debugging without separate .pdb files

## Requirements

- **.NET 8.0 SDK** or later
- For macOS universal binary: `lipo` tool (included with Xcode Command Line Tools)
- For cross-compilation: You can build for any platform from any platform (e.g., build Linux binaries on Windows)

## Troubleshooting

### Build fails with "Platform not supported"

Make sure you have the .NET 8.0 SDK installed:
```bash
dotnet --version
```

### Permission denied on macOS/Linux

Make the scripts executable:
```bash
chmod +x build-all.sh build-macos.sh build-linux.sh
```

### ReadyToRun compilation warnings

ReadyToRun (R2R) compilation may show warnings when cross-compiling. These are generally safe to ignore. The resulting binaries will still work correctly on the target platform.

## CI/CD Integration

These scripts are designed to work in CI/CD environments:

**GitHub Actions example:**
```yaml
- name: Build all platforms
  run: pwsh ./build-all.ps1 Release artifacts

- name: Upload artifacts
  uses: actions/upload-artifact@v3
  with:
    name: vbx-binaries
    path: artifacts/
```

**Cross-platform CI:**
```yaml
jobs:
  build-windows:
    runs-on: windows-latest
    steps:
      - run: .\build-windows.ps1

  build-macos:
    runs-on: macos-latest
    steps:
      - run: ./build-macos.sh

  build-linux:
    runs-on: ubuntu-latest
    steps:
      - run: ./build-linux.sh
```