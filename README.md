# ScreenSnap

ScreenSnap is a Windows tray utility for capturing and annotating a region of the screen. The executable uses Windows Forms on the .NET Framework included with Windows 10 and 11. It needs no installer or administrator privileges.

## First run

1. Put `ScreenSnap.exe` in a permanent folder and run it once. ScreenSnap registers itself to start at sign-in for the current user.
2. Press **Alt+1**, then drag a region on the frozen screen. Press **Esc** or right-click to cancel. You can also choose **Capture region** from the tray menu.
3. In the editor, pick the **Scribble**, **Line**, **Rectangle**, **Ellipse**, or **Text** icon. Hold **Shift** while dragging a rectangle or ellipse to draw a square or circle. With **Text**, click the image and type directly on it. **Enter** places the text, **Shift+Enter** adds a new line, and **Esc** cancels. Click a crayon swatch to set the ink color and use the **Thin–Thick** slider for stroke and text size. The arrow icon or **Ctrl+Z** reverts an edit. The editor renders text from outlines at the display scale and exports antialiased text at the capture's original pixel dimensions.
4. Choose **Make transparent** and click a background pixel. ScreenSnap removes matching pixels connected to that pixel horizontally or vertically. The default tolerance is 5% per RGB channel and is adjustable in Settings.

The capture is copied immediately and saved as a PNG. After an edit, ScreenSnap updates the same PNG and copies the edited image after 500 ms of inactivity. Hover over the save status in the footer to see the file path or error details. Closing the editor or exiting ScreenSnap flushes any pending edit without a save prompt. The clipboard includes PNG for apps that support transparency and a bitmap for general pasting.

The editor's close button leaves the capture hotkey active. Choose **Exit ScreenSnap** from the tray to quit. Use **Settings** to change the hotkey, folder, filename pattern, or startup preference.

## File naming

By default ScreenSnap writes to the **ScreenSnap** folder inside your Windows Pictures folder. The default pattern is `{yyyy}\{mm}-{dd}-{nnn}`. A capture on November 21, 2026 can produce `2026\11-21-001.png`. Subfolders are created as needed.

| Token | Value |
| --- | --- |
| `{yyyy}` | Four-digit local year |
| `{mm}`, `{MM}` | Two-digit month |
| `{dd}` | Two-digit day |
| `{hh}` | 24-hour clock hour |
| `{min}` | Two-digit minute |
| `{ss}` | Two-digit second |
| `{nnn}` | Persistent sequence with at least three digits |

The pattern `{hh}{mm}{ss}` also works. A lowercase `{mm}` immediately following an hour token in the same path segment means minutes. Use `{MM}` to explicitly request the month. The sequence continues across dates and restarts. ScreenSnap skips existing names and appends `.png` if needed.

## Build

Run `build.ps1` from PowerShell. The script uses the Windows .NET Framework C# compiler. The source and icon are included.
