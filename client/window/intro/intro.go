package intro

import (
	"log"

	"fyne.io/fyne"
	"fyne.io/fyne/widget"
)

type Intent int

const (
	IntentQuit Intent = iota
	IntentSignIn
)

func Show(app fyne.App) Intent {
	title := widget.NewLabel("JamCast")
	title.TextStyle = fyne.TextStyle{
		Bold: true,
	}

	intent := IntentQuit

	log.Println("intro: showing intro window")

	w := app.NewWindow("JamCast")
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
			widget.NewButton("Quit", func() {
				log.Println("intro: user chose 'quit' intent")
				intent = IntentQuit
				w.Close()
			}),
		),
	))

	w.CenterOnScreen()

	w.ShowAndRun()

	return intent
}
