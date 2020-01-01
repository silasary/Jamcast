package download

import (
	"image/color"

	"fyne.io/fyne"
	"fyne.io/fyne/canvas"
	"fyne.io/fyne/theme"
	"fyne.io/fyne/widget"
)

type titleArea struct {
	widget.BaseWidget
	image *canvas.Image
}

func (t *titleArea) CreateRenderer() fyne.WidgetRenderer {
	r := &renderer{
		image: t.image,
	}
	r.Refresh()
	return r
}

func (t *titleArea) MinSize() fyne.Size {
	t.ExtendBaseWidget(t)
	return t.image.Size()
}

type renderer struct {
	image   *canvas.Image
	objects []fyne.CanvasObject
}

func (r *renderer) Layout(size fyne.Size) {
	//r.image.Resize(size)
}
func (r *renderer) MinSize() fyne.Size {
	return r.image.MinSize()
}

func (r *renderer) Refresh() {
	r.image.FillMode = canvas.ImageFillContain
	r.Layout(r.image.Size())
	canvas.Refresh(r.image)
	r.objects = []fyne.CanvasObject{
		r.image,
	}
}
func (r *renderer) BackgroundColor() color.Color {
	return theme.PrimaryColor()
}
func (r *renderer) Objects() []fyne.CanvasObject {
	return r.objects
}
func (r *renderer) Destroy() {
}
