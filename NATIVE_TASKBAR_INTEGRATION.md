# Native Taskbar Integration

This project cannot honestly implement the requested Win11 taskbar text block as a lightweight WinForms EXE.

## What failed

The WinForms overlay and `SetParent` attempts were removed. Logs showed valid taskbar coordinates and handles, but Windows 11 did not reliably render or preserve the external window in the taskbar layer.

## Real implementation path

Build a native Explorer component, not a normal app window:

1. Use C++ with Visual Studio Build Tools and Windows SDK.
2. Implement a COM in-process desk band object with `IDeskBand`, `IObjectWithSite`, and `IPersistStream`, or target a classic-taskbar compatibility layer such as ExplorerPatcher.
3. Register the component as a desk band under `CATID_DeskBand`.
4. Render the balance in the band child window and update it from a small background worker.
5. Provide installer/uninstaller scripts for COM registration and Explorer restart.

## Current blocker

This machine has no `cl.exe`, `msbuild.exe`, or C++ build environment available. Only the .NET Framework C# compiler is present, which is suitable for the stable tray EXE but not for a reliable native Explorer component.

References:

- Microsoft band objects: https://learn.microsoft.com/en-us/windows/win32/shell/band-objects
- Microsoft application desktop toolbars: https://learn.microsoft.com/en-us/windows/win32/shell/application-desktop-toolbars
