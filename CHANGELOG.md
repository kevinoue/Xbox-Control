# Changelog

## 1.0.1 (2026-11-16)

### Changed
- The settings app and its Start menu shortcut are now called **Xbox Control for SolidWorks**,
  with "for SolidWorks" shown next to the title.
- Credit for Kevin Ouellette in the settings app, the installer, Windows' Apps list (publisher) and
  SolidWorks' Add-ins list.
- The settings app shows its version and a link to the project page.

## 1.0.0 (2026-11-15)

A rewrite. The controller now drives the SolidWorks view directly from a SolidWorks add-in
instead of sending keystrokes to the Walk-through tool.

### Added
- SolidWorks add-in (SolidWorks 2023 and newer): every axis analog and simultaneous, about 60
  updates per second, only active while SolidWorks is the foreground window, state shown in the
  SolidWorks status bar.
- Walk-through style default layout: left stick forward/back and sideways, triggers up/down,
  right stick orbit, bumpers roll.
- Button actions: zoom to fit, zoom to selection, standard views, perspective, precision mode,
  pause, open settings; and hold-to-move actions for any button.
- Xbox Control Settings app: remap every button and stick, invert axes, adjust speeds and stick
  response. The control you press lights up. Changes apply instantly without restarting
  SolidWorks. Opens from the Start menu or SolidWorks' Tools menu.
- Per-user MSI installer (no administrator rights).
- Developer tools: `AddinDev` (load/unload, in-process self-test, simulated button presses),
  `SwViewBench` (API timing), `register-addin.ps1`, `build-installer.ps1`.

### Fixed (keystroke Walk-through app)
- Zoom in/out were swapped.
- Movements that share Shift/Alt (e.g. rise + strafe) no longer turn into a different movement.
- Keys are released on controller disconnect, focus loss, exit and crash (no more stuck keys).
- Controller found in any slot; reconnects automatically.
- Keys only go to SolidWorks, never to another window or a text box.
- Moved from SharpDX to XInput directly and from .NET 6 to .NET 10.
