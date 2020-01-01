package main

import (
	"io"
	"log"
	"os"

	"fyne.io/fyne"
	"fyne.io/fyne/app"
	"github.com/getlantern/systray"

	"gitlab.com/redpointgames/jamcast/image"
	"gitlab.com/redpointgames/jamcast/client/window/logs"
)

var clientApp fyne.App
var systrayReady chan bool

const enableSystray = true

func main() { 
	// split logs between stdout and our internal logging
	log.SetOutput(io.MultiWriter(os.Stdout, logs.GetInMemoryLogBuffer()))

	log.Println("starting JamCast")

	clientApp = app.New()
	clientApp.SetIcon(fyne.NewStaticResource("icon.png", image.IconPNG))

	if enableSystray {
		systrayReady = make(chan bool)
		go func() {
			systray.Run(func() {
				systray.SetIcon(image.Icon)
				systray.SetTitle("JamCast")
				systray.SetTooltip("JamCast - Not signed in")

				systrayReady <- true
			}, func() {})
		}()
	}

	go func() {
		stageIntro()
	}()

	for {
		clientApp.Run()

		clientApp = app.New()
		clientApp.SetIcon(fyne.NewStaticResource("icon.png", image.IconPNG))
	}

	if enableSystray {
		systray.Quit()
	}
}

func shutdown() {
	log.Println("shutdown: abnormal shutdown!")

	if enableSystray {
		systray.Quit()
	} else {
		clientApp.Quit()
	}
}