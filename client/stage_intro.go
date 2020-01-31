package main

import (
	"fmt"
	"log"

	"github.com/dgrijalva/jwt-go"
	"github.com/getlantern/systray"

	"gitlab.com/redpointgames/jamcast/auth"
	"gitlab.com/redpointgames/jamcast/client/shutdown"
	"gitlab.com/redpointgames/jamcast/client/window/intro"
	"gitlab.com/redpointgames/jamcast/client/window/logs"
)

func stageIntro() {
	var signIn *systray.MenuItem
	var signOut *systray.MenuItem

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
			shutdown.Shutdown()
		}

		if enableSystray {
			systray.SetTooltip(fmt.Sprintf(
				"JamCast - %s",
				token.Claims.(jwt.MapClaims)["site.displayName"],
			))

			signIn.Hide()
		}

		signOut.Show()

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
		signOut = systray.AddMenuItem("Sign out", "Sign out of JamCast")
		signOut.Hide()
		exit := systray.AddMenuItem("Exit", "Exit JamCast")

		go func() {
			for range signIn.ClickedCh {
				if showSignIn() {
					return
				}
			}
		}()
		go func() {
			for range signOut.ClickedCh {
				// erase auth and relaunch
				auth.EraseCredentials()

				// todo: relaunch
				log.Println("todo: relaunch needed")
				shutdown.Shutdown()
			}
		}()
		go func() {
			for range showLogs.ClickedCh {
				logs.Show(clientApp)
			}
		}()
		go func() {
			for range exit.ClickedCh {
				shutdown.Shutdown()
			}
		}()

		// Prompt for sign in on start up.
		signIn.ClickedCh <- struct{}{}
	} else {
		if !showSignIn() {
			shutdown.Shutdown()
		}
	}
}
