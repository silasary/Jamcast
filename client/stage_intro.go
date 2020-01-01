package main

import (
	"fmt"
	"log"

	"github.com/dgrijalva/jwt-go"
	"github.com/getlantern/systray"

	"gitlab.com/redpointgames/jamcast/auth"
	"gitlab.com/redpointgames/jamcast/client/window/intro"
	"gitlab.com/redpointgames/jamcast/client/window/l"gs
)

func stageIntro() {
	var signIn *systray.MenuItem

	showSignIn := func() bool {
		log.Println("intro: sign in requested")

		// User wants to sign in.
		if !auth.HasCredentials() {
			intent := intro.Show(clientApp)
			if intent == intro.IntentClose {
				// User has decided to not sign in right now.
				return false
			}
		}

		token, err := auth.GetCredentials()
		if err != nil {
			log.Println(err)
			shutdown()
		}

		if enableSystray {
			systray.SetTooltip(fmt.Sprintf(
				"JamCast - %s",
				token.Claims.(jwt.MapClaims)["site.displayName"],
			))

			signIn.Hide()
		}

		stageDownloadOBS(token)

		return true
	}

	if enableSystray {
		<-systrayReady

		title := systray.AddMenuItem("JamCast", "")
		title.Disable()
		systray.AddSeparator()
		showLogs := systray.AddMenuItem("Show logs", "Show the client logs")
		signIn = systray.AddMenuItem("Sign in", "Sign into JamCast")

		go func() {
			for range signIn.ClickedCh {
				if showSignIn() {
					return
				}
			}
		}()
		go func() {
			for range showLogs.ClickedCh {
				logs.Show(clientApp)
			}
		}()

		// Prompt for sign in on start up.
		signIn.ClickedCh <- struct{}{}
	} else {
		if !showSignIn() {
			shutdown()
		}
	}
}
