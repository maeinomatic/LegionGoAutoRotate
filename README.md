# Legion Go Auto Rotate

<p align="center">
  <img src="assets/LegionGoAutoRotate-AppIcon/AppIcon-256.png" alt="Legion Go Auto Rotate icon" width="128">
</p>

A lightweight Windows tray application that restores automatic screen rotation on the **Lenovo Legion Go 2**.

Windows normally exposes automatic rotation and Rotation Lock through its built-in autorotation pipeline. That pipeline depends on the device's form-factor, slate-mode, display, and sensor state.

The Legion Go 2 can identify to Windows as a **gaming handheld** through the `DeviceForm` value used for Microsoft's Full Screen Experience (FSE), rather than as a regular tablet/slate device. In that handheld configuration, Windows can leave the normal autorotation controls disabled or unavailable even though apps can still read the built-in `SimpleOrientationSensor`.

Legion Go Auto Rotate works around this without changing `DeviceForm`. It reads the orientation sensor directly and rotates the built-in display itself, preserving Windows' handheld/FSE behavior.

## Features

* Automatic rotation between landscape and portrait orientations
* Uses the Legion Go 2's built-in orientation sensor
* Runs silently in the Windows system tray
* Start or stop automatic rotation from the tray menu
* Pauses automatic rotation while either Legion Go 2 controller is attached by default
* Optional tray setting to allow rotation with controllers attached
* Optional per-user Start with Windows support
* Active, paused, and controller-blocked tray status icons
* About dialog with version and repository information
* Does not change the Windows `DeviceForm` setting
* Does not automatically start with Windows unless explicitly enabled
* No .NET installation required when using the self-contained release

## Installation

Download `LegionGoAutoRotate.exe` from the [Releases](../../releases) page.

No installation is required. Simply place the executable somewhere on your Legion Go 2 and run it.

## Usage

When started, Legion Go Auto Rotate appears in the Windows system tray and begins monitoring rotation state.

Right-click the tray icon to access:

* **Start Auto Rotate** — enables automatic screen rotation
* **Stop Auto Rotate** — pauses automatic screen rotation
* **Rotate with Controllers Attached** — allows automatic rotation even while controllers are attached
* **Start with Windows** — toggles per-user Windows startup
* **Open Diagnostics Folder** — opens the local diagnostics folder
* **About Legion Go Auto Rotate** — shows version and repository information
* **Exit** — closes the application

By default, automatic rotation runs when the controllers are detached and pauses when either controller is attached. This avoids rotating the display during normal handheld use with attached controllers. Enable **Rotate with Controllers Attached** from the tray menu if you want the display to rotate in that state too.

When automatic rotation is stopped or paused by controller attachment, the current display orientation remains unchanged.

## Requirements

* Lenovo Legion Go 2
* Windows 11
* Windows must expose the device's `SimpleOrientationSensor`

The application is currently built for Windows x64.

## Building from Source

The project requires the .NET 8 SDK.

```powershell
dotnet build
```

To create a self-contained single-file executable:

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None
```

The resulting executable can be found in `bin\Release\net8.0-windows10.0.19041.0\win-x64\publish`.

## Current Limitations

The application targets the active built-in Legion Go 2 display and leaves external displays untouched. If the built-in display is disabled, automatic rotation safely does nothing.

Controller attachment detection is based on the Legion Go 2 controller HID reports. Until the controller state is known, automatic rotation is held in the controller-blocked state.

This project was created specifically for the Lenovo Legion Go 2. Other Windows devices with a compatible orientation sensor may work, but are currently untested.
