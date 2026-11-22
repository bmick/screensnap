# ScreenSnap

A lightweight screenshot editor for Windows 10 and 11. Press a shortcut, drag to capture, add a quick annotation, and paste. ScreenSnap runs from the system tray as a single executable.

## Quick start

1. Run `ScreenSnap.exe`. No installer or administrator access is required.
2. Press **Alt+1** and drag over the area you want to capture. Press **Esc** to cancel.
3. Mark it up with freehand drawing, lines, shapes, or text. Use **Ctrl+Z** to undo.

ScreenSnap copies the capture to the clipboard and saves a PNG in **Pictures → ScreenSnap**. Edits automatically update both. Right-click the tray icon and choose **Settings** to change the shortcut, save folder, filename pattern, or startup behavior.

## Project goal

Make everyday screenshots as quick and easy as possible. A feature earns its place when it makes capturing, marking up, or sharing a screenshot faster without making the app harder to use.

## Build

From the repository folder on Windows, run `.\build.ps1` in PowerShell. It creates `ScreenSnap.exe` using the Windows .NET Framework C# compiler.
