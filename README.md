# Keep Awake

A tiny Windows system-tray utility that **nudges the mouse a few pixels every
10 minutes** (default) so Windows never considers the session idle — which
prevents the workstation from locking and the screen from turning off.

Written in C# (WinForms). It lives in the **taskbar notification area** with
its own logo: a computer mouse with motion chevrons on a teal badge
(grey badge with pause bars when paused).

![logo](assets/KeepAwake.ico)

## Features

- Runs from the taskbar notification area (tray) with a custom-drawn logo.
- Nudges the mouse 3–6 pixels in a random direction and puts it right back
  250 ms later — imperceptible, but it resets the Windows idle timer.
- Uses `SendInput` (the real input channel), so it works against idle-lock,
  screensaver and display-off timeouts.
- Right-click tray menu:
  - **Enabled** — pause/resume without exiting.
  - **Nudge interval** — 5, 10, 15 or 30 minutes.
  - **Nudge mouse now** — instant test.
  - **Start with Windows** — registers in `HKCU\...\Run` (current user only,
    no admin needed).
  - **Exit**.
- Live countdown tooltip: *"Keep Awake — next nudge in 09:59"*.
- Single-instance guard.
- The exe icon (Explorer, taskbar) matches the tray logo.

## Build

**No SDK or Visual Studio needed** — `build.cmd` uses the C# compiler that
ships with every Windows installation:

```cmd
build.cmd
```

Output: `dist\KeepAwake.exe`

Alternatively, with the .NET 8 SDK installed:

```cmd
dotnet build src\KeepAwake\KeepAwake.csproj
```

To regenerate the multi-size `assets\KeepAwake.ico` after changing the
design:

```cmd
powershell -ExecutionPolicy Bypass -File tools\GenerateIcon.ps1
```

Both the tray logo and the .ico share the same vector design (mouse
silhouette + scroll wheel + motion chevrons on a rounded gradient badge),
drawn in code — no image assets required.

## Run

Double-click `dist\KeepAwake.exe`. The icon appears in the notification area
and a balloon confirms the interval.

Tip: to keep the logo always visible on the taskbar, drag the icon out of
the overflow flyout, or set it to *On* under
`Settings → Personalization → Taskbar → Other system tray icons`.

## Notes

- Only nudges while **Enabled** is checked (default on).
- Some corporate policies lock the machine for other reasons (e.g. on
  screensaver activation regardless of input, or via a security agent that
  monitors synthetic input); in those cases an IT-granted exemption is
  the only reliable fix.
- The nudge is a real (synthetic) mouse input event: it also resets
  "away" status in chat apps that rely on input activity.
