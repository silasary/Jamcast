package env

import (
	"os"
	"path/filepath"
	"runtime"
)

func GetAppPath() string {
	appPath := getAppPath()
	os.MkdirAll(appPath, 0755)
	return appPath
}

func getAppPath() string {
	switch runtime.GOOS {
	case "linux":
	case "darwin":
		return filepath.Join(os.Getenv("HOME"), ".jamcast")
	case "windows":
		return filepath.Join(os.Getenv("LOCALAPPDATA"), ".jamcast")
	}
	panic("unsupported platform")
}
