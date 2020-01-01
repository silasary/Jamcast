package download

import (
	"bytes"
	"fmt"
	"image/png"
	"log"
	"path/filepath"

	"fyne.io/fyne"
	"fyne.io/fyne/canvas"
	"fyne.io/fyne/widget"
	"github.com/dgrijalva/jwt-go"
	"gitlab.com/redpointgames/jamcast/env"
)

type task func(callback func(label string, progress float64, infinite bool)) error

func GetOBSInstallPath() string {
	return filepath.Join(
		env.GetAppPath(),
		"jc-obs-studio",
	)
}

func NeedsVisibleDownloadScreen() bool {
	obsPath := GetOBSInstallPath()
	obsPluginPath := filepath.Join(
		obsPath,
		"obs-plugins",
		"64bit",
		"obs-websocket.dll",
	)

	obsInstalled, err := pathExists(obsPath)
	if err != nil {
		return true
	}
	obsPluginInstalled, err := pathExists(obsPluginPath)
	if err != nil {
		return true
	}

	return !obsInstalled || !obsPluginInstalled
}

func Show(app fyne.App, token *jwt.Token, imageData []byte) {
	siteName := token.Claims.(jwt.MapClaims)["site.displayName"].(string)

	log.Println("download: creating image area")

	pngImg, err := png.Decode(bytes.NewReader(imageData))
	if err != nil {
		log.Println("download: unable to load title image")
		return
	}
	pngBounds := pngImg.Bounds()
	log.Println("png image size: ", pngImg.Bounds())

	img := canvas.NewImageFromImage(pngImg)
	size := fyne.NewSize(pngBounds.Dx(), pngBounds.Dy())
	img.Resize(size)
	img.SetMinSize(size)
	log.Println("image size: ", img.Size())
	log.Println("image min size: ", img.MinSize())

	title := &titleArea{
		image: img,
	}

	log.Println("download: showing download window")

	status := widget.NewLabel("Please wait...")
	progress := widget.NewProgressBar()
	progress.Hide()
	progressInfinite := widget.NewProgressBarInfinite()

	w := app.NewWindow(fmt.Sprintf("%s - JamCast", siteName))
	w.SetContent(widget.NewVBox(
		title,
		status,
		progress,
		progressInfinite,
	))

	log.Println("download: centering download window")

	w.CenterOnScreen()

	tasks := []task{
		taskDownloadOBS,
	}

	log.Println("download: starting task execution")

	go func() {
		for i, task := range tasks {
			log.Printf("download: starting task #%d\n", i)

			err := task(func(label string, p float64, infinite bool) {
				status.SetText(label)
				progress.SetValue(p)
				if infinite {
					if !progressInfinite.Visible() {
						progressInfinite.Show()
						progress.Hide()
					}
				} else {
					if !progress.Visible() {
						progressInfinite.Hide()
						progress.Show()
					}
				}
			})
			if err != nil {
				log.Printf("download: error during task #%d: %v\n", i, err)

				status.SetText(fmt.Sprintf(
					"Error during operation '%s': %v",
					status.Text,
					err,
				))
				return
			}
		}

		w.Close()
	}()

	log.Println("download: showing and running download window")

	w.Show()

	c := make(chan bool)
	w.SetOnClosed(func() {
		c <- true
	})

	<-c

	log.Println("download: window complete")

}
