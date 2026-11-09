# Stickies_Window

A copy of macOS Stickies for Windows, with soft colors. Small single `.exe`, nothing to install.

## Build

1. Download this repo.
2. Double-click `build.bat`.
3. Run `Stickies_Window.exe`.

Needs Windows 10 or 11 (.NET Framework 4.8 is already built in). Rounded corners need Windows 11.

## Use

| Action | How |
|---|---|
| Menu | Right-click a note |
| New note | `Ctrl+N`, or the tray icon |
| Close note (deletes it) | `Ctrl+W` or the left box. Asks to save first |
| Move | Drag the title bar |
| Resize | Drag any edge |
| Collapse / expand | Double-click the title bar, the right box, or `Ctrl+M` |
| Float on Top | `Ctrl+Alt+F` |
| Translucent | `Ctrl+Alt+T` |
| Bold / Italic / Underline | `Ctrl+B` / `Ctrl+I` / `Ctrl+U` |
| Bigger / Smaller text | `Ctrl++` / `Ctrl+-` |
| Fonts / Text color | `Ctrl+T` / `Ctrl+Shift+C` |
| Align left / center / right | `Ctrl+L` / `Ctrl+E` / `Ctrl+R` |
| Find / Find next | `Ctrl+F` / `Ctrl+G` |
| Print | `Ctrl+P` |
| Images | Paste, or drag an image file onto a note |
| Show all notes | Click the tray icon |
| Quit | Right-click the tray icon > Quit Stickies_Window |

Colors: Yellow, Blue, Green, Pink, Purple, Gray.

## Where notes are saved

`%AppData%\Stickies_Window\` — one `.rtf` file per note, plus `notes.xml` for position, size, color and settings. Saved automatically.
