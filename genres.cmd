@echo off
cd %~dp0
echo //+build windows > image\icon_windows.go
echo. >> image\icon_windows.go
type image\icon.ico | go run github.com/cratonica/2goarray Icon image >> image\icon_windows.go
type image\cast.ico | go run github.com/cratonica/2goarray CastIdle image >> image\icon_windows.go
echo //+build linux darwin > image\icon_unix.go
echo. >> image\icon_unix.go
type image\icon.png | go run github.com/cratonica/2goarray Icon image >> image\icon_unix.go
type image\cast.png | go run github.com/cratonica/2goarray CastIdle image >> image\icon_unix.go
type image\icon.png | go run github.com/cratonica/2goarray IconPNG image > image\icon_all.go
type image\cast.png | go run github.com/cratonica/2goarray CastIdlePNG image >> image\icon_all.go
go run github.com/akavel/rsrc -arch amd64 -ico image\icon.ico -manifest client\app.manifest -o client\rsrc.syso