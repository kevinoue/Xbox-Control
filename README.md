# Xbox Control for SolidWorks

Move around SOLIDWORKS models with an Xbox controller. Two pieces:

- **SolidWorks add-in** (`Xbox Control.Addin`): drives the view directly through the SolidWorks
  API from inside SolidWorks. Every axis is analog and works at the same time; no keystrokes.
- **Walk-through app** (`Xbox Control`): sends the Walk-through tool's keyboard shortcuts.

Use one or the other at a time, not both.

## SolidWorks add-in

| Controller | Action |
|---|---|
| Right stick | Orbit (the model follows the stick) |
| Left stick | Pan (moves the camera: push right to see further right) |
| RT / LT | Zoom in / out (pull harder = faster) |
| LB / RB | Roll left / right |
| A | Zoom to fit |
| B | Zoom to selection (click a part or face first) |
| Y | Isometric |
| D-pad up / down / left / right | Top / Front / Left / Right view |
| X | Perspective on / off |
| Left stick click | Precision mode (quarter speed) on / off |
| Start | Pause / resume the controller |

The view only moves while SolidWorks is the active window. The SolidWorks status bar shows
the controller state and what each button just did. Drawings pan and zoom only.

Speeds, stick curve and axis inversion are in `%APPDATA%\XboxControl\addin.ini` (created on
first load). Problems are logged to `%LOCALAPPDATA%\XboxControl\addin.log`.

### Install (development build)

```
dotnet build "Xbox Control.Addin"
.\tools\register-addin.ps1
```

The script registers the add-in for the current user (no administrator rights) and copies it
to `%LOCALAPPDATA%\XboxControl\Addin\<version>\`, so SolidWorks never locks the build output.
SolidWorks loads it at startup. To load it into a running SolidWorks that hasn't loaded it yet
(a running SolidWorks keeps whichever version it loaded first, so a new build needs a restart):

```
tools\AddinDev\bin\Debug\net10.0-windows\AddinDev.exe load "%LOCALAPPDATA%\XboxControl\Addin\<version>\XboxControlAddin.dll"
```

If SolidWorks runs as administrator, run the script from an elevated PowerShell instead (it then
registers for all users). `.\tools\register-addin.ps1 -Unregister` removes it.

### Speed

Measured on SolidWorks 2023 SP5 with a 155-part assembly (`AddinDev selftest`):

| | From a separate app | Add-in |
|---|---|---|
| Rotate + pan + zoom in one update | ~35 ms (~30 updates/s) | 5.6 ms (~57 updates/s) |

## Walk-through app

Drives the SOLIDWORKS Walk-through tool by sending its keyboard shortcuts.

### Controls

| Controller | Walk-through action | Keys sent |
|---|---|---|
| Left stick | Walk forward/back, strafe left/right | W / S, A / D |
| Right stick X | Turn left/right | Shift+A / Shift+D |
| Right stick Y | Look up/down | Alt+Up / Alt+Down |
| RT / LT | Move up/down | Shift+Up / Shift+Down |
| RB / LB | Zoom in/out | Shift+Z / Z |
| D-pad up/down | Speed +/- | = / - |

Keys follow the official Walk-through key list (SOLIDWORKS 2023/2024 help, "Walk-through Control Panel").

### Using it

1. Open an assembly, then **View > Lights and Cameras > Add Walk-through**, and click **Start Walk-through**.
2. Run `Xbox Control.exe`. It finds the controller in any slot and keeps looking if it disconnects.
3. Click into SolidWorks (or just move a stick while this app has focus and it hands focus to SolidWorks).

Keys are only sent while SolidWorks has focus, and never while a text box in SolidWorks has focus.
All keys are released when the controller disconnects, when focus leaves SolidWorks, and on exit or crash.

If SolidWorks runs as administrator, this app must too (Windows blocks key input to elevated apps).

### Known limits of the keystroke approach

The Walk-through uses one Shift and one Alt key for several actions, so some combinations can't
be sent at the same time without changing meaning. The app picks one and drops the other:

- Strafing pauses while turning, rising/falling, or zooming in (A/D under Shift means *turn*).
- Walking pauses while looking up/down (Alt+W/S would open menus).
- Turn/rise vs. look: whichever started first keeps control unless the other is pushed much harder.

The add-in doesn't have these limits.

## Build

Requires the .NET 10 SDK on Windows and SolidWorks installed (the add-in and tools reference its
API from the default install folder; override with `-p:SolidWorksApiDir=...`).

```
dotnet build "Xbox Control.sln"
dotnet test "Xbox Control.sln"
```

The add-in targets .NET Framework 4.8 (required by SolidWorks add-ins); the reference assemblies
come from NuGet at build time. The walk-through app has no NuGet or SolidWorks dependencies.

## Developer tools

- `tools/AddinDev`: `load` / `unload` the add-in in a running SolidWorks, show its `status`, and
  `selftest [combo|rotate|pan|zoom] [seconds] [--nobatch]`, which feeds synthetic stick input through
  the add-in's own loop inside SolidWorks and restores the view afterwards.
- `tools/SwViewBench`: times the same view calls from a separate process (below).

## SwViewBench (API speed test)

With SolidWorks open and a part or assembly active:

```
tools\SwViewBench\bin\Debug\net10.0-windows\SwViewBench.exe            # view tests, view restored afterwards
tools\SwViewBench\bin\Debug\net10.0-windows\SwViewBench.exe --camera   # also tests a floating camera (adds a camera; use a copy)
```

Results are printed and saved to `bench-results.txt` next to the exe.
