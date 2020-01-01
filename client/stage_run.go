package main

import (
	"io/ioutil"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"syscall"
	"time"

	"github.com/dgrijalva/jwt-go"
	"gitlab.com/redpointgames/jamcast/client/window/download"
)

var hasWrittenProfile bool

func stageRun(token *jwt.Token) {
	log.Println("launch obs: starting OBS management task...")

	go manageOBS(token)
}

func manageOBS(token *jwt.Token) {

	log.Println("launch obs: waiting for controller connection...")

	attempts := 0
	for !hasWrittenProfile {
		attempts++

		if attempts > 300 {
			log.Println("launch obs: waiting for controller connection...")

			// todo: log progress
			// progress.UnsetProgress($"Connecting to Controller...\nYou have been waiting for {txt}.  Try relaunching Jamcast, or contacting support.");
		} else {
			log.Println("launch obs: waiting for controller connection...")

			// todo: lighter log progress
			// progress.UnsetProgress("Connecting to Controller...");
		}

		time.Sleep(time.Second)
	}

	log.Println("launch obs: OBS profiles have been written")

	log.Println("launch obs: killing any existing OBS process...")

	var cmd *exec.Cmd
	defer func() {
		if cmd != nil && cmd.Process != nil {
			cmd.Process.Kill()
		}
	}()
	cmd = exec.Command("taskkill", "/f", "/t", "/im", "obs64.exe")
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	cmd.Start()
	cmd.Wait()
	cmd = nil

	for {
		log.Println("launch obs: accepting OBS license agreement...")
		acceptLicense()

		log.Println("launch obs: waiting for filesystem sync...")
		time.Sleep(time.Second * 1000)

		log.Println("launch obs: starting OBS...")
		cmd = exec.Command(
			filepath.Join(
				download.GetOBSInstallPath(),
				"bin",
				"64bit",
				"obs64.exe",
			),
		)
		cmd.SysProcAttr = &syscall.SysProcAttr{
			HideWindow: true,
		}
		cmd.Stdin = os.Stdin
		cmd.Stdout = os.Stdout
		cmd.Stderr = os.Stderr
		cmd.Start()
		cmd.Wait()
		cmd = nil
	}
}

func acceptLicense() {
	ini := filepath.Join(
		os.Getenv("APPDATA"),
		"obs-studio",
		"global.ini",
	)
	if _, err := os.Stat(ini); os.IsNotExist(err) {
		os.MkdirAll(
			filepath.Join(
				os.Getenv("APPDATA"),
				"obs-studio",
			),
			os.ModePerm,
		)
	}
	ioutil.WriteFile(
		ini,
		[]byte("[General]\r\nLicenseAccepted=true\r\nFirstRun=true"),
		os.ModePerm,
	)
}
