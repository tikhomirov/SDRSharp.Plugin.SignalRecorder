# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a signal recorder plugin for SDRSharp (SDR#), a software-defined radio application. The plugin records IQ signal data when signal levels exceed a configurable threshold, with options to save I/Q components, modulus, and argument data to CSV files.

**Environment Note**: This is a Windows application (.NET 9.0-windows with Windows Forms), but Claude Code is running in WSL on Windows. All build commands and file paths need to account for this cross-platform development setup.

## Architecture

The plugin follows SDRSharp's plugin architecture with three main components:

1. **SignalRecorderPlugin.cs** - Main plugin entry point implementing `ISharpPlugin` and `ICanLazyLoadGui`
2. **SignalRecorderProcessor.cs** - Core signal processing logic implementing `IIQProcessor` for real-time IQ data processing
3. **ProcessorPanel.cs** - Windows Forms UI panel for plugin configuration and controls
4. **PlotForm.cs** - Plotting window using ScottPlot for visualizing recorded data

The processor is registered as a stream hook with `ProcessorType.DecimatedAndFilteredIQ` to receive processed IQ data from SDRSharp.

## Key Dependencies

- **SDRSharp SDK**: References to `SDRSharp.Common.dll`, `SDRSharp.PanView.dll`, and `SDRSharp.Radio.dll` from `../lib/` directory
- **ScottPlot**: Version 4.1.41 for data visualization
- **Target Framework**: .NET 9.0 Windows (upgraded from .NET 7)

## Build Instructions

The project requires the SDRSharp SDK to be installed in the parent directory. Since Claude Code runs in WSL but this is a Windows application, use Windows executables:

```bash
# Build the project from WSL (requires .NET 9.0 SDK for Windows)
dotnet.exe build

# For release build
dotnet.exe build -c Release

# Alternative: Use MSBuild directly
msbuild.exe SDRSharp.Plugin.SignalRecorder.csproj

# For release build with MSBuild
msbuild.exe SDRSharp.Plugin.SignalRecorder.csproj -p:Configuration=Release
```

Output files are placed in:

- Debug: `../Debug/net9.0-windows/`
- Release: `../Release/net9.0-windows/`

**WSL Development Notes**:

- Use `.exe` extensions for Windows executables when calling from WSL
- The project targets `net9.0-windows` which requires Windows-specific .NET runtime
- File paths use Windows-style paths (`/mnt/c/Users/...`) when accessed from WSL
- The application itself must run on Windows (not in WSL) since it uses Windows Forms and targets Windows-specific APIs

## Development Setup

1. Download the SDR# SDK for Plugin Developers (.NET 7+) from Airspy website
2. Clone this repository inside the sdrplugins solution folder
3. Ensure the project folder is next to the SDK `lib` folder
4. The required SDRSharp DLLs should be available in `../lib/`

**WSL/Windows Development Workflow**:

- Source code editing: Can be done from WSL using Claude Code
- Building: Use Windows .NET SDK via `.exe` commands from WSL
- Testing: Must be done on Windows since the plugin integrates with SDRSharp (Windows application)
- File paths: Use `/mnt/c/Users/...` format when accessing Windows files from WSL

## Signal Processing Architecture

The `SignalRecorderProcessor.Process()` method runs in real-time on the IQ data stream:

- Calculates signal power in dB: `20 * log10(modulus)`
- Triggers recording when signal exceeds threshold
- Supports automatic recording mode and manual control
- Implements file splitting based on maximum recording time per file
- Saves configurable data columns (I, Q, Modulus, Argument) to CSV

## File Structure

```
SDRSharp.Plugin.SignalRecorder/
├── PlotForm.cs/Designer.cs/resx    # Data visualization window
├── ProcessorPanel.cs/Designer.cs/resx  # Main UI panel
├── SignalRecorderPlugin.cs         # Plugin interface
└── SignalRecorderProcessor.cs      # Core signal processing
```

## Data Output Format

CSV files are saved with timestamp-based filenames (`SigRec_yyyyMMddHHmmssff.csv`) containing:

- Sample time [ms] (always included)
- I component (if enabled)
- Q component (if enabled)
- Modulus (if enabled)
- Argument (if enabled)

## Configuration Options

Key settings managed through the UI:

- Signal threshold (dB) for recording trigger
- Recording duration and maximum time per file
- Output folder selection
- Data components to save (I, Q, Modulus, Argument)
- Auto-record vs manual recording modes

## Testing and Deployment

**Running/Testing from Command Line**:
The project includes launch settings that start SDRSharp with the plugin loaded. Use `cmd.exe` to launch from WSL:

```bash
# After building, run SDRSharp with plugin from WSL (Debug build):
cmd.exe /c "cd /d ..\Debug\net9.0-windows && ..\..\bin\SDRSharp.exe"

# Or for Release build:
cmd.exe /c "cd /d ..\Release\net9.0-windows && ..\..\bin\SDRSharp.exe"
```

This matches the Visual Studio launch configuration in `Properties/launchSettings.json`:

- Changes to the build output directory (Debug/Release)
- Runs SDRSharp.exe from the bin folder
- Plugin is automatically loaded from the working directory

**Plugin Installation**:

- Copy `SDRSharp.Plugin.SignalRecorder.dll` to SDRSharp plugins folder
- Copy ScottPlot dependencies (`ScottPlot.dll`, `ScottPlot.WinForms.dll`) to SDRSharp folder
- Plugin will appear in SDRSharp's plugin panel