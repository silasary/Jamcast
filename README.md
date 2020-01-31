# JamCast

This is the client software for [JamHost](https://jamhost.org/). It enables attendees to stream their
desktops to projectors set up at your event or game jam.

This repository contains the source code for JamCast, in case you want to audit
it or build it from scratch.

## Downloading

If you're just looking to run JamCast at your event, you can download JamCast
from your event page in JamHost. **You do not need to build JamCast from 
source in order to use it.**

## Building the client from source

If you want to build it from source, follow the instructions below:

### Windows

```
# Install dependencies
choco install -y golang mingw

# Build it
go build -o JamCast.exe -ldflags -H=windowsgui ./client
```

### macOS

Coming soon (once we have a macOS build machine).

### Linux

```
# Install dependencies
sudo add-apt-repository ppa:longsleep/golang-backports
sudo apt-get update
sudo apt install -y golang-go libx11-dev libglfw3-dev libglfw3 libxcursor-dev libxi-dev libgtk-3-dev libwebkit2gtk-4.0-dev libappindicator3-dev
sudo ldconfig

# Build it
go build -o ./jamcast ./client

```

## Building the controller from source

Open the controller solution file in Visual Studio 2019, with the .NET Core 3.0 SDK and .NET Desktop workload installed. The controller software only supports Windows.

### Running the controller

To run the controller, perform the following steps:

- Download and extract [OBS 22.0](https://github.com/obsproject/obs-studio/releases/download/22.0.2/OBS-Studio-22.0.2-Full-x64.zip) somewhere on the desktop.
- Download [OBS WebSocket 4.5.0](https://github.com/Palakis/obs-websocket/releases/download/4.5.0/obs-websocket-4.5.0-Windows.zip) and copy it's contents over the top of the OBS folder.
- Launch OBS, skip through the wizard. If you get prompted to upgrade versions, just say "Skip Version".
- In "Scene Collection", select "Import" and then locate the "Scene.json" file underneath "JamCast.Controller". Switch to that scene (it will probably be called "Untitled (2)").
- Launch the controller software.

## Workflow

If you need to re-generate resources, such as the application icons or the
generated gRPC protocol files, you can run `genres` on Windows.
