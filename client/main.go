package main

import (
	"log"

	"fyne.io/fyne/app"
	"gitlab.com/redpointgames/jamcast/auth"
	"gitlab.com/redpointgames/jamcast/client/window/intro"
)

func main() {
	log.Println("starting JamCast")

	app := app.New()

	if !auth.HasCredentials() {
		intent := intro.Show(app)
		if intent == intro.IntentQuit {
			app.Quit()
			return
		}
	}

	token, err := auth.GetCredentials()
	if err != nil {
		log.Fatalln(err)
	}

	log.Println(token)

	log.Println("normal exit")
}
