package main

import (
	"log"

	"github.com/dgrijalva/jwt-go"

	"gitlab.com/redpointgames/jamcast/client/theme"
	"gitlab.com/redpointgames/jamcast/client/window/download"
)

func stageDownloadOBS(token *jwt.Token) {
	if download.NeedsVisibleDownloadScreen() {
		imageData, err := theme.LoadImage(token)
		if err != nil {
			log.Println(err)
			shutdown()
		}

		download.Show(clientApp, token, imageData)
	}

	stageRun(token)
}
