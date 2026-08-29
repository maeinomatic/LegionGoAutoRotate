# Legion Go Auto Rotate

A lightweight Windows tray application that restores automatic screen rotation on the **Lenovo Legion Go 2**.

Recent Windows 11 gaming-handheld behavior can leave the Legion Go 2 without the normal Windows auto-rotation functionality and Rotation Lock control. Legion Go Auto Rotate works around this by reading the device's built-in orientation sensor directly and rotating the display accordingly.

## Features

* Automatic rotation between landscape and portrait orientations
* Uses the Legion Go 2's built-in orientation sensor
* Runs silently in the Windows system tray
* Start or stop automatic rotation from the tray icon
* Pauses automatic rotation while both Legion Go 2 controllers are attached by default
* Optional tray setting to allow rotation with controllers attached
* Optional per-user Start with Windows support
* Does not change the Windows `DeviceForm` setting
* Does not automatically start with Windows unless explicitly enabled
* No .NET installation required when using the self-contained release

## Installation

Download `LegionGoAutoRotate.exe` from the [Releases](../../releases) page.

No installation is required. Simply place the executable somewhere on your Legion Go 2 and run it.

## Usage

When started, Legion Go Auto Rotate appears in the Windows system tray and automatic rotation is enabled.

Right-click the tray icon to access:

* **Start Auto Rotate** — enables automatic screen rotation
* **Stop Auto Rotate** — pauses automatic screen rotation
* **Rotate with Controllers Attached** — allows automatic rotation even while both controllers are attached
* **Start with Windows** — toggles per-user Windows startup
* **Open Diagnostics Folder** — opens the local diagnostics folder
* **Exit** — closes the application

When automatic rotation is stopped, the current display orientation remains unchanged.

By default, automatic rotation runs when the controllers are detached and pauses when both controllers are attached. This avoids rotating the display during normal handheld use with attached controllers. Enable **Rotate with Controllers Attached** from the tray menu if you want the display to rotate in that state too.

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

The resulting executable can be found in the `publish` directory.

## Current Limitations

The application targets the active built-in Legion Go 2 display and leaves external displays untouched. If the built-in display is disabled, automatic rotation safely does nothing.

Controller attachment detection is based on the Legion Go 2 controller HID reports. If the controller status cannot be read, automatic rotation is allowed rather than blocked.

This project was created specifically for the Lenovo Legion Go 2. Other Windows devices with a compatible orientation sensor may work, but are currently untested.
