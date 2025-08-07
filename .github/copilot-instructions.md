# FH5 Trainer Development Guide

FH5 Trainer is a Windows WPF application for Forza Horizon 5 game modification/training. It includes gamepad support and memory manipulation capabilities.

**ALWAYS reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.**

## Critical Platform Requirements

⚠️ **WINDOWS ONLY**: This application can only be built and run on Windows systems due to WPF (Windows Presentation Foundation) dependency.

- **Cannot build on Linux/Unix**: Missing Windows Desktop workload
- **Cannot run on non-Windows**: WPF is Windows-specific
- **Cannot functionally test on Linux**: GUI requires Windows

## Working Effectively

### System Prerequisites (Windows Only)
- .NET 8.0 SDK or later
- Windows 10/11 (required for WPF)
- Visual Studio 2022 or Visual Studio Code with C# extension

### Repository Structure
```
sauce/
├── MA_FH5Trainer/          # Main WPF application
│   ├── MA_FH5Trainer.csproj
│   ├── Views/              # XAML UI files
│   ├── Cheats/             # Game modification logic
│   └── Resources/          # Hotkey/gamepad management
└── Memory/                 # Memory manipulation library
    └── Memory.csproj
```

### Build Commands (Windows Only)

**NEVER CANCEL: All build operations must complete. Set timeouts to 120+ seconds.**

1. **Restore dependencies** (50 seconds):
   ```bash
   cd sauce
   dotnet restore MA_FH5Trainer.sln
   ```

2. **Build Memory library only** (2 seconds):
   ```bash
   cd sauce
   dotnet build Memory/Memory.csproj --configuration Release
   ```

3. **Build full solution** (Windows only):
   ```bash
   cd sauce
   dotnet build MA_FH5Trainer.sln --configuration Release
   ```

4. **Publish single-file executable** (Windows only):
   ```bash
   cd sauce
   dotnet publish MA_FH5Trainer/MA_FH5Trainer.csproj --configuration Release --self-contained true --runtime win-x64
   ```

5. **Clean build artifacts** (1 second):
   ```bash
   cd sauce
   dotnet clean Memory/Memory.csproj
   ```

### Expected Build Issues on Non-Windows
- ❌ **Main application build fails**: Missing Windows Desktop workload
- ✅ **Memory library builds successfully**: Platform-agnostic
- ⚠️ **Expected warnings**: Nullable reference types, strong naming

## Testing and Validation

### No Automated Tests
- **No unit test infrastructure exists**
- **No CI/CD workflows configured**
- **Manual testing required**

### Manual Testing Scenarios (Windows Only)
When making changes to the trainer:

1. **Launch the application**:
   - Run the built executable or `dotnet run` from MA_FH5Trainer directory
   - Verify the WPF window opens correctly

2. **Test gamepad functionality**:
   - Connect an Xbox-compatible controller
   - Navigate to hotkey settings
   - Test gamepad button assignment
   - Verify gamepad input detection

3. **Test memory manipulation** (with Forza Horizon 5 running):
   - Launch Forza Horizon 5
   - Enable any cheat feature
   - Verify the feature affects the game as expected

4. **Test hotkey binding**:
   - Set keyboard and gamepad hotkeys
   - Test activation in-game
   - Verify hotkey persistence across application restarts

## Development Workflow

### Code Organization
- **Cheats/**: Game modification logic (261 C# files total)
- **Memory/**: Low-level memory access utilities
- **Views/**: WPF XAML user interface
- **Resources/Keybinds/**: Hotkey and gamepad management

### Key Dependencies
- **WPF**: Windows-only UI framework
- **SharpDX.XInput**: Gamepad input support
- **MahApps.Metro**: Modern WPF styling
- **CommunityToolkit.Mvvm**: MVVM pattern support

### Making Changes
1. **Edit source code** in appropriate directory structure
2. **Build Memory library first** if modifying memory operations
3. **Build full solution** to verify changes (Windows only)
4. **Test manually** with actual game running
5. **No linting/formatting tools configured** - maintain existing code style

## Common Tasks

### When modifying gamepad support:
- **Always test** with `GlobalHotkey.cs` changes
- **Verify** gamepad button enumeration in `GamepadButton.cs`
- **Check** gamepad polling in `GamepadManager.cs`

### When adding new cheats:
- **Follow existing patterns** in `Cheats/` directory
- **Implement** both keyboard and gamepad hotkey support
- **Test** with Memory library for game process access

### When updating UI:
- **Modify XAML files** in `Views/` directory
- **Follow WPF/MVVM patterns** established in codebase
- **Test** UI responsiveness and layout

## Build Artifacts to Ignore

Always exclude these from commits:
```
**/bin/
**/obj/
**/*.user
**/publish/
```

## Platform Limitations Summary

| Task | Windows | Linux/Unix |
|------|---------|------------|
| Restore dependencies | ✅ 50s | ✅ 50s |
| Build Memory library | ✅ 2s | ✅ 2s |
| Build main application | ✅ | ❌ Missing Windows Desktop |
| Run application | ✅ | ❌ WPF requires Windows |
| Functional testing | ✅ | ❌ Cannot test GUI |
| Clean build | ✅ | ✅ (Memory only) |

**Always develop and test on Windows systems. Use non-Windows systems only for Memory library development or code review.**