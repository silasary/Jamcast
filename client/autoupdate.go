package main

import (
    "log"
    "github.com/blang/semver"
    "github.com/rhysd/go-github-selfupdate/selfupdate"
)

func doSelfUpdate() {
	if version == "0.0" {
		return
	}

	v := semver.MustParse(version)
    latest, err := selfupdate.UpdateSelf(v, "silasary/Jamcast")
    if err != nil {
        log.Println("Binary update failed:", err)
        return
    }
    if latest.Version.Equals(v) {
        // latest version is the same as current version. It means current binary is up to date.
        log.Println("Current binary is the latest version", version)
    } else {
        log.Println("Successfully updated to version", latest.Version)
        log.Println("Release note:\n", latest.ReleaseNotes)
    }
}
