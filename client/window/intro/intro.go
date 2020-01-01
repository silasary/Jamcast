package intro

import (
	"log"

	"gitlab.com/redpointgames/jamcast/image"

	"fyne.io/fyne"
	"fyne.io/fyne/widget"
)

type Intent int

const (
	IntentClose Intent = iota
	IntentSignIn
)

func Show(app fyne.App) Intent {
	title := widget.NewLabel("JamCast")
	title.TextStyle = fyne.TextStyle{
		Bold: true,
	}

	intent := IntentClose

	log.Println("intro: showing intro window")

	w := app.NewWindow("JamCast")
	w.SetIcon(fyne.NewStaticResource("icon.png", image.IconPNG))
	w.SetContent(widget.NewVBox(
		title,
		widget.NewLabel(
			"Welcome to JamCast! This software will allow you to \n"+
				"stream your desktop to projectors at the event. To \n"+
				"get started, we need to sign you in first.",
		),
		widget.NewHBox(
			widget.NewButton("Sign In", func() {
				log.Println("intro: user chose 'sign in' intent")
				intent = IntentSignIn
				w.Close()
			}),
			widget.NewButton("Close", func() {
				log.Println("intro: user chose 'close' intent")
				intent = IntentClose
				w.Close()
			}),
		),
	))

	w.CenterOnScreen()

	w.Show()

	c := make(chan bool)
	w.SetOnClosed(func() {
		c <- true
	})

	<-c

	return intent
}
