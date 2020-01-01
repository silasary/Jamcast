@echo off
cd %~dp0
echo Icon - Windows - Host
echo //+build windows > image\icon_windows.go
echo. >> image\icon_windows.go
type image\icon.ico | go run github.com/cratonica/2goarray Icon image >> image\icon_windows.go
echo Icon - Windows - Cast
echo //+build windows > image\cast_windows.go
echo. >> image\cast_windows.go
type image\cast.ico | go run github.com/cratonica/2goarray CastIdle image >> image\cast_windows.go
echo Icon - UNIX - Host
echo //+build linux darwin > image\icon_unix.go
echo. >> image\icon_unix.go
type image\icon.png | go run github.com/cratonica/2goarray Icon image >> image\icon_unix.go
echo Icon - UNIX - Cast
echo //+build linux darwin > image\cast_unix.go
echo. >> image\cast_unix.go
type image\cast.png | go run github.com/cratonica/2goarray CastIdle image >> image\cast_unix.go
echo Icon - All Platforms - Host
type image\icon.png | go run github.com/cratonica/2goarray IconPNG image > image\icon_all.go
echo Icon - All Platforms - Cast
type image\cast.png | go run github.com/cratonica/2goarray CastIdlePNG image > image\cast_all.go
echo ResX - Windows
go run github.com/akavel/rsrc -arch amd64 -ico image\icon.ico -manifest client\app.manifest -o client\rsrc.syso
echo gRPC - Go SDK
go get -u github.com/golang/protobuf/protoc-gen-go
deps\protoc\bin\protoc --go_out=plugins=grpc:proto jamcast.proto