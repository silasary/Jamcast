# JamCast

This is the client software for [JamHost](https://jamhost.org/). It enables attendees to stream their
desktops to projectors set up at your event or game jam.

This repository contains the source code for JamCast, in case you want to audit
it or build it from scratch.

## Downloading

If you're just looking to run JamCast at your event, you can download JamCast
from your event page in JamHost. **You do not need to build JamCast from 
source in order to use it.**

## Building from source

If you want to build it from source, follow the instructions below:

### Windows

```
# Install dependencies
choco install -y golang mingw

# Build it
go build -o JamCast.exe -ldflags -H=windowsgui ./client
go build -o JamCast-Controller.exe ./controller
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
go build -o ./jamcast-controller ./controller

```

## Workflow

If you need to re-generate resources, such as the application icons or the
generated gRPC protocol files, you can run `genres` on Windows.
