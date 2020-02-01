package download

import (
	"archive/zip"
	"encoding/json"
	"fmt"
	"io"
	"log"
	"net/http"
	"net/url"
	"os"
	"path/filepath"
	"strings"

	"gitlab.com/redpointgames/jamcast/auth"
	"gitlab.com/redpointgames/jamcast/env"
)

type downloadURLs struct {
	OBSWindowsDownloadURL                string
	OBSWebSocketPluginWindowsDownloadURL string
}

func taskDownloadOBS(callback func(label string, progress float64, infinite bool)) error {
	callback("Checking for OBS and plugins...", 0, true)

	log.Println("run: fetching download URLs")
	downloadUrlsJson, err := auth.MakeRequest("/jamcast/downloadurls", url.Values{})
	if err != nil {
		log.Printf("error: can't fetch download URLs: %v", err)
		return err
	}
	var d downloadURLs
	err = json.Unmarshal([]byte(downloadUrlsJson), &d)
	if err != nil {
		log.Printf("error: can't unmarshal download URLs JSON: %v", err)
		return err
	}

	log.Printf("download URLs are: %+v\n", d)

	obsPath := GetOBSInstallPath()
	obsWebSocketPluginPath := filepath.Join(
		obsPath,
		"obs-plugins",
		"64bit",
		"obs-websocket.dll",
	)
	obsDownloadPath := filepath.Join(env.GetAppPath(), "obs-24.zip")
	obsWebSocketPluginDownloadPath := filepath.Join(env.GetAppPath(), "obs-websocket.zip")

	obsInstalled, err := pathExists(obsPath)
	if err != nil {
		return err
	}
	obsWebSocketPluginInstalled, err := pathExists(obsWebSocketPluginPath)
	if err != nil {
		return err
	}

	if obsInstalled && obsWebSocketPluginInstalled {
		callback("OBS and dependencies are installed.", 1, false)
		return err
	}

	err = downloadAndUnpack(
		d.OBSWindowsDownloadURL,
		obsDownloadPath,
		obsPath,
		func(op string, progress float64, infinite bool) {
			callback(fmt.Sprintf("%s OBS...", op), progress, infinite)
		},
	)
	if err != nil {
		return err
	}

	err = downloadAndUnpack(
		d.OBSWebSocketPluginWindowsDownloadURL,
		obsWebSocketPluginDownloadPath,
		obsPath,
		func(op string, progress float64, infinite bool) {
			callback(fmt.Sprintf("%s OBS WebSocket plugin...", op), progress, infinite)
		},
	)
	if err != nil {
		return err
	}

	return nil
}

func pathExists(path string) (bool, error) {
	_, err := os.Stat(path)
	if err != nil {
		if os.IsNotExist(err) {
			return false, nil
		}

		return false, err
	}
	return true, nil
}

type progressMonitor struct {
	total    uint64
	current  uint64
	callback func(progress float64)
}

func (wc *progressMonitor) Write(p []byte) (int, error) {
	n := len(p)
	wc.current += uint64(n)
	wc.callback(float64(wc.current) / float64(wc.total))
	return n, nil
}

func download(url string, zipPath string, callback func(op string, progress float64, infinite bool)) error {
	if url == "" {
		return fmt.Errorf("no url provided")
	}

	out, err := os.Create(zipPath)
	if err != nil {
		return err
	}
	defer out.Close()

	resp, err := http.Get(url)
	if err != nil {
		return err
	}
	defer resp.Body.Close()

	counter := progressMonitor{
		total: uint64(resp.ContentLength),
		callback: func(progress float64) {
			callback("Downloading", progress, false)
		},
	}
	_, err = io.Copy(out, io.TeeReader(resp.Body, &counter))
	if err != nil {
		return err
	}
	return nil
}

func unpack(url string, zipPath string, directory string, callback func(op string, progress float64, infinite bool)) error {
	z, err := zip.OpenReader(zipPath)
	if err != nil {
		return err
	}

	for _, f := range z.File {
		fpath := filepath.Join(directory, f.Name)
		if !strings.HasPrefix(fpath, filepath.Clean(directory)+string(os.PathSeparator)) {
			return fmt.Errorf("illegal file path in ZIP archive: %s", fpath)
		}

		if f.FileInfo().IsDir() {
			os.MkdirAll(fpath, os.ModePerm)
			continue
		}

		if err = os.MkdirAll(filepath.Dir(fpath), os.ModePerm); err != nil {
			return err
		}

		outFile, err := os.OpenFile(fpath, os.O_WRONLY|os.O_CREATE|os.O_TRUNC, f.Mode())
		if err != nil {
			return err
		}

		rc, err := f.Open()
		if err != nil {
			return err
		}

		_, err = io.Copy(outFile, rc)

		// Close the file without defer to close before next iteration of loop
		outFile.Close()
		rc.Close()

		if err != nil {
			return err
		}
	}

	return nil
}

func downloadAndUnpack(url string, zipPath string, directory string, callback func(op string, progress float64, infinite bool)) error {
	didDownload := false
	if exists, err := pathExists(zipPath); !exists || err != nil {
		err = download(url, zipPath, callback)
		if err != nil {
			return err
		}
		didDownload = true
	}

	callback("Installing", 0, true)

	err := unpack(url, zipPath, directory, callback)
	if err != nil {
		if didDownload {
			return err
		} else {
			// cached file might be partial download or corrupt
			os.Remove(zipPath)

			// retry
			return downloadAndUnpack(url, zipPath, directory, callback)
		}
	}

	return nil
}
