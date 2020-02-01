package main

import (
	"io/ioutil"
	"log"
	"os"
	"os/exec"
	"path/filepath"

	"github.com/blang/semver"
	"github.com/rhysd/go-github-selfupdate/selfupdate"
	"gitlab.com/redpointgames/jamcast/env"
)

func doSelfUpdate() {
	if version == "0.0.0" {
		return
	}

	v := semver.MustParse(version)

	latest, found, err := selfupdate.DetectLatest("silasary/Jamcast")
	if err != nil {
		log.Println("autoupdate: error occurred while detecting version:", err)
		return
	}

	if !found || latest.Version.LTE(v) {
		log.Println("autoupdate: current version is the latest")
		return
	}

	// self update library doesn't move things out the way
	// by default, which makes things not work on Windows.
	// copy ourselves to a safe location, then relaunch to complete
	// the actual update
	exe, err := os.Executable()
	if err != nil {
		log.Println("autoupdate: could not locate executable path")
		return
	}

	tmpExe := filepath.Join(env.GetAppPath(), "JamCast-tmp.exe")
	err = copyFile(exe, tmpExe)
	if err != nil {
		log.Println("autoupdate: could not make copy of executable")
		return
	}

	cmd := exec.Command(
		tmpExe,
		"--self-update",
		exe,
	)
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	cmd.Start()

	log.Println("autoupdate: launched copy of executable to complete self update")
	os.Exit(0)
}

func finishSelfUpdate(targetPath string) {
	doFinishSelfUpdate(targetPath)

	// launch new version
	log.Println("autoupdate: launching new version")
	cmd := exec.Command(
		targetPath,
	)
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	cmd.Start()
}

func doFinishSelfUpdate(targetPath string) {
	log.Println("autoupdate: finishing self update")

	latest, _, err := selfupdate.DetectLatest("silasary/Jamcast")
	if err != nil {
		log.Println("autoupdate: error occurred while detecting version:", err)
		return
	}

	if err := selfupdate.UpdateTo(latest.AssetURL, targetPath); err != nil {
		log.Println("autoupdate: error occurred while updating binary:", err)
		return
	}
	log.Println("autoupdate: successfully updated to version", latest.Version)
}

func copyFile(sourceFile string, destinationFile string) error {
	input, err := ioutil.ReadFile(sourceFile)
	if err != nil {
		return err
	}

	err = ioutil.WriteFile(destinationFile, input, 0644)
	if err != nil {
		return err
	}

	return nil
}
