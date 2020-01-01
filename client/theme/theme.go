package theme

import (
	"fmt"
	"io"
	"io/ioutil"
	"log"
	"net/http"
	"os"
	"path/filepath"

	"github.com/dgrijalva/jwt-go"
	"gitlab.com/redpointgames/jamcast/env"
)

func LoadImage(token *jwt.Token) ([]byte, error) {
	log.Println("theme: loading image")

	urlID := token.Claims.(jwt.MapClaims)["site.urlID"].(string)
	imageURL := token.Claims.(jwt.MapClaims)["site.imageURL"].(string)

	appPath := env.GetAppPath()
	imagePath := filepath.Join(appPath, fmt.Sprintf("%s.png", urlID))
	data, err := ioutil.ReadFile(imagePath)
	if os.IsNotExist(err) {
		log.Println("theme: need to download ", imageURL)

		err = downloadFile(imagePath, imageURL)
		if err != nil {
			return nil, err
		}
		data, err = ioutil.ReadFile(imagePath)
		if err != nil {
			return nil, err
		}
	} else if err != nil {
		return nil, err
	}

	log.Println("theme: image data loaded")

	return data, nil
}

func downloadFile(filepath string, url string) error {
	// Get the data
	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	// Create the file
	out, err := os.Create(filepath)
	if err != nil {
		return err
	}
	defer out.Close()

	// Write the body to file
	_, err = io.Copy(out, resp.Body)
	return err
}
