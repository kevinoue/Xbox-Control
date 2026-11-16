# Xbox Control for SolidWorks

Drive the SOLIDWORKS Walk-through with an Xbox controller.

## Controls

| Controller | Walk-through action | Keys sent |
|---|---|---|
| Left stick | Walk forward/back, strafe left/right | W / S, A / D |
| Right stick X | Turn left/right | Shift+A / Shift+D |
| Right stick Y | Look up/down | Alt+Up / Alt+Down |
| RT / LT | Move up/down | Shift+Up / Shift+Down |
| RB / LB | Zoom in/out | Shift+Z / Z |
| D-pad up/down | Speed +/- | = / - |

Keys follow the official Walk-through key list (SOLIDWORKS 2023/2024 help, "Walk-through Control Panel").

## Using it

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

The planned fix is driving the SolidWorks view directly through its API (no keystrokes), which
allows every axis at once with analog speed. `tools/SwViewBench` measures whether that is fast
enough from a separate app.

## Build

Requires the .NET 10 SDK on Windows.

```
dotnet build "Xbox Control.sln"
dotnet test "Xbox Control.sln"
```

The main app has no NuGet or SolidWorks dependencies. `tools/SwViewBench` references the SolidWorks
interop from the default install folder (override with `-p:SolidWorksApiDir=...`).

## SwViewBench (API speed test)

With SolidWorks open and a part or assembly active:

```
tools\SwViewBench\bin\Debug\net10.0-windows\SwViewBench.exe            # view tests, view restored afterwards
tools\SwViewBench\bin\Debug\net10.0-windows\SwViewBench.exe --camera   # also tests a floating camera (adds a camera; use a copy)
```

Results are printed and saved to `bench-results.txt` next to the exe.
