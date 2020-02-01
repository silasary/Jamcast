package main

import (
	"context"
	"fmt"
	"io"
	"io/ioutil"
	"log"
	"net"
	"net/url"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"time"

	"github.com/dgrijalva/jwt-go"
	"github.com/getlantern/systray"
	"gitlab.com/redpointgames/jamcast/auth"
	"gitlab.com/redpointgames/jamcast/client/platform"
	"gitlab.com/redpointgames/jamcast/client/shutdown"
	"gitlab.com/redpointgames/jamcast/client/window/download"
	"gitlab.com/redpointgames/jamcast/image"
	jamcast "gitlab.com/redpointgames/jamcast/proto"
	"google.golang.org/grpc"
)

var hasWrittenProfile bool

func stageRun(token *jwt.Token) {
	log.Println("run: starting OBS management task...")

	go manageOBS(token)

	connectToController(token)
}

func connectToController(token *jwt.Token) {

	shouldRun := true
	shutdown.RegisterShutdownHandler(func() {
		shouldRun = false
	})

	for shouldRun {

		log.Println("run: fetching controller IP")
		controllerIPAddressAndPort, err := auth.MakeRequest("/jamcast/controllerip", url.Values{})
		if err != nil {
			log.Printf("error: can't fetch controller IP and port: %v", err)
			time.Sleep(time.Second)
			continue
		}

		log.Println("run: got controller IP", controllerIPAddressAndPort)

		conn, err := grpc.Dial(controllerIPAddressAndPort, grpc.WithInsecure())
		if err != nil {
			log.Printf("error: can't connect to controller: %v", err)
			time.Sleep(time.Second)
			continue
		}

		log.Println("run: connected to controller")

		client := jamcast.NewControllerClient(conn)
		stream, err := client.Connect(context.Background(), &jamcast.ClientRequest{})
		if err != nil {
			log.Printf("error: can't connect to controller: %v", err)
			time.Sleep(time.Second)
			conn.Close()
			continue
		}
		for shouldRun {
			// we are now connected, set up OBS profiles initially
			controllerIPAddress, _, _ := net.SplitHostPort(controllerIPAddressAndPort)
			if obsWriteAllProfiles(controllerIPAddress) {
				// forcibly restart OBS because we changed profile information
				log.Println("run: killing OBS because configuration changed")
				killOBS()
			}
			hasWrittenProfile = true

			if enableSystray {
				systray.SetIcon(image.CastIdle)
			}

			// wait for disconnection from controller
			// todo: set up channel for shutdown listen, then select over both
			//       the Recv call and the shutdown channel
			_, err := stream.Recv()
			if err == io.EOF {
				log.Printf("run: disconnected from controller")
				time.Sleep(time.Second)
				break
			}
			if err != nil {
				log.Printf("error: error while connected to controller: %v", err)
				time.Sleep(time.Second)
				break
			}
		}

		conn.Close()
	}
}

func obsWriteAllProfiles(ipAddress string) bool {
	log.Println("obs: writing all profiles")
	var dirty bool
	dirty = obsWriteProfile("Primary", ipAddress, 1234) || dirty
	dirty = obsWriteProfile("Secondary", ipAddress, 1235) || dirty
	dirty = obsWriteScene("JamCast") || dirty
	return dirty
}

func obsWriteProfile(suffix string, ipAddress string, port int) bool {
	name := fmt.Sprintf("Jamcast-%s", suffix)
	dname := fmt.Sprintf("Jamcast%s", suffix)
	profileDir := filepath.Join(
		os.Getenv("APPDATA"),
		"obs-studio",
		"basic",
		"profiles",
	)
	os.MkdirAll(filepath.Join(profileDir, dname), os.ModePerm)
	contents := []string{
		"[General]",
		fmt.Sprintf("Name=%s", name),
		"[Video]",
		"BaseCX=1920",
		"BaseCY=1080",
		"OutputCX=1280",
		"OutputCY=720",
		"[Output]",
		"Mode=Advanced",
		"[AdvOut]",
		"TrackIndex=1",
		"RecType=FFmpeg",
		"RecTracks=1",
		"FFOutputToFile=false",
		fmt.Sprintf("FFURL=udp://%s:%d?pkt_size=1316", ipAddress, port),
		"FFFormat=mpegts",
		"FFFormatMimeType=video/MP2T",
		"FFExtension=ts",
		"FFIgnoreCompat=true",
		"FFVEncoderId=28",
		"FFVEncoder=libx264",
		"FFAEncoderId=86018",
		"FFAEncoder=aac",
		"FFAudioTrack=1",
	}
	contentsJoined := strings.Join(contents, "\r\n")
	profilePath := filepath.Join(profileDir, dname, "basic.ini")
	if data, err := ioutil.ReadFile(profilePath); err != nil || string(data) != contentsJoined {
		log.Println("obs: writing profile configuration for ", suffix)
		ioutil.WriteFile(profilePath, []byte(contentsJoined), os.ModePerm)
		return true
	}
	return false
}

func obsWriteScene(name string) bool {
	sceneDir := filepath.Join(
		os.Getenv("APPDATA"),
		"obs-studio",
		"basic",
		"scenes",
	)
	os.MkdirAll(sceneDir, os.ModePerm)
	sceneJson := filepath.Join(sceneDir, fmt.Sprintf("%s.json", name))
	if data, err := ioutil.ReadFile(sceneJson); err != nil || string(data) != string(SceneJSON) {
		log.Println("obs: writing scene configuration")
		ioutil.WriteFile(sceneJson, SceneJSON, os.ModePerm)
		return true
	}
	return false
}

func killOBS() {
	cmd := exec.Command("taskkill", "/f", "/t", "/im", "obs64.exe")
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
	platform.HideWindow(cmd)
	cmd.Start()
	cmd.Wait()
}

func manageOBS(token *jwt.Token) {

	shouldRun := true
	shutdown.RegisterShutdownHandler(func() {
		shouldRun = false
	})

	log.Println("run: waiting for controller connection...")

	attempts := 0
	for !hasWrittenProfile && shouldRun {
		attempts++

		if attempts > 300 {
			log.Println("run: waiting for controller connection...")

			// todo: log progress
			// progress.UnsetProgress($"Connecting to Controller...\nYou have been waiting for {txt}.  Try relaunching Jamcast, or contacting support.");
		} else {
			log.Println("run: waiting for controller connection...")

			// todo: lighter log progress
			// progress.UnsetProgress("Connecting to Controller...");
		}

		time.Sleep(time.Second)
	}
	if !shouldRun {
		return
	}

	log.Println("run: OBS profiles have been written")

	log.Println("run: killing any existing OBS process...")

	killOBS()

	var cmd *exec.Cmd
	shutdownOBS := func() {
		if cmd != nil && cmd.Process != nil {
			cmd.Process.Kill()
		}
	}
	defer shutdownOBS()
	shutdown.RegisterShutdownHandler(func() {
		shutdownOBS()
	})

	for shouldRun {
		log.Println("run: accepting OBS license agreement...")
		acceptLicense()

		log.Println("run: waiting for filesystem sync...")
		time.Sleep(time.Second)

		log.Println("run: starting OBS...")
		cmd = exec.Command(
			filepath.Join(
				download.GetOBSInstallPath(),
				"bin",
				"64bit",
				"obs64.exe",
			),
			"--minimize-to-tray",
		)
		cmd.Dir = filepath.Join(
			download.GetOBSInstallPath(),
			"bin",
			"64bit",
		)
		cmd.Stdin = os.Stdin
		cmd.Stdout = os.Stdout
		cmd.Stderr = os.Stderr
		cmd.Start()
		cmd.Wait()
		cmd = nil
	}
}

func acceptLicense() {
	ini := filepath.Join(
		os.Getenv("APPDATA"),
		"obs-studio",
		"global.ini",
	)
	if _, err := os.Stat(ini); os.IsNotExist(err) {
		os.MkdirAll(
			filepath.Join(
				os.Getenv("APPDATA"),
				"obs-studio",
			),
			os.ModePerm,
		)
	}
	ioutil.WriteFile(
		ini,
		[]byte(
			"[General]\r\nLicenseAccepted=true\r\nFirstRun=true\r\nEnableAutoUpdates=false\r\n"+
				"[BasicWindow]\r\nSysTrayEnabled=true\r\nSysTrayWhenStarted=true\r\nSysTrayMinimizeToTray=true\r\n"+
				"[Basic]\r\nSceneCollection=JamCast\r\nSceneCollectionFile=JamCast\r\n",
		),
		os.ModePerm,
	)
}

