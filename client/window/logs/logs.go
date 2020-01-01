package logs

import (
	"bytes"
	"io"
	"log"
	"time"

	"fyne.io/fyne"
	"fyne.io/fyne/widget"
	"gitlab.com/redpointgames/jamcast/image"
)

var shown bool
var inMemoryLogBuffer *bytes.Buffer

func init() {
	inMemoryLogBuffer = new(bytes.Buffer)
}

func GetInMemoryLogBuffer() io.Writer {
	return inMemoryLogBuffer
}

func Show(app fyne.App) {
	if shown {
		return
	}

	shown = true

	log.Println("logs: opening logs window")

	desiredSize := fyne.NewSize(768, 500)

	label := widget.NewLabel(string(inMemoryLogBuffer.Bytes()))
	scroll := widget.NewScrollContainer(label)
	scroll.Offset = fyne.NewPos(0, scroll.Content.Size().Height-scroll.Size().Height)

	go func() {
		for shown {
			label.SetText(string(inMemoryLogBuffer.Bytes()))
			label.Refresh()
			scroll.Offset = fyne.NewPos(0, scroll.Content.Size().Height-scroll.Size().Height)
			time.Sleep(250 * time.Millisecond)
		}
	}()

	w := app.NewWindow("JamCast Logs")
	w.SetIcon(fyne.NewStaticResource("icon.png", image.IconPNG))
	w.SetContent(scroll)

	w.Resize(desiredSize)
	w.CenterOnScreen()

	w.SetOnClosed(func() {
		log.Println("logs: closed logs window")
		shown = false
	})

	w.Show()
}
