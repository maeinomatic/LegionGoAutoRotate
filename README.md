# Legion Go Auto Rotate

A lightweight Windows tray application that restores automatic screen rotation on the **Lenovo Legion Go 2**.

Recent Windows 11 gaming-handheld behavior can leave the Legion Go 2 without the normal Windows auto-rotation functionality and Rotation Lock control. Legion Go Auto Rotate works around this by reading the device's built-in orientation sensor directly and rotating the display accordingly.

## Features

* Automatic rotation between landscape and portrait orientations
* Uses the Legion Go 2's built-in orientation sensor
* Runs silently in the Windows system tray
* Start or stop automatic rotation from the tray icon
* Does not change the Windows `DeviceForm` setting
* Does not automatically start with Windows
* No .NET installation required when using the self-contained release

## Installation

Download `LegionGoAutoRotate.exe` from the [Releases](../../releases) page.

No installation is required. Simply place the executable somewhere on your Legion Go 2 and run it.

## Usage

When started, Legion Go Auto Rotate appears in the Windows system tray and automatic rotation is enabled.

Right-click the tray icon to access:

* **Start Auto Rotate** — enables automatic screen rotation
* **Stop Auto Rotate** — pauses automatic screen rotation
* **Exit** — closes the application

When automatic rotation is stopped, the current display orientation remains unchanged.

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

This project was created specifically for the Lenovo Legion Go 2. Other Windows devices with a compatible orientation sensor may work, but are currently untested.
