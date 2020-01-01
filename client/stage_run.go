package main

import (
	"context"
	"fmt"
	"io"
	"io/ioutil"
	"log"
	"os"
	"os/exec"
	"path/filepath"
	"strings"
	"syscall"
	"time"

	"github.com/dgrijalva/jwt-go"
	"gitlab.com/redpointgames/jamcast/client/shutdown"
	"gitlab.com/redpointgames/jamcast/client/window/download"
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

		// todo: get IP address from JamHost API
		// todo: make sure we re-fetch this after on each connection attempt
		controllerIPAddress := "127.0.0.1"

		conn, err := grpc.Dial(fmt.Sprintf("%s:8080", controllerIPAddress), grpc.WithInsecure())
		if err != nil {
			log.Printf("error: can't connect to controller: %v", err)
			time.Sleep(time.Second)
		}
		defer conn.Close()

		log.Println("run: connected to controller")

		client := jamcast.NewControllerClient(conn)
		stream, err := client.Connect(context.Background(), &jamcast.ClientRequest{})
		if err != nil {
			log.Printf("error: can't connect to controller: %v", err)
			time.Sleep(time.Second)
			continue
		}
		for shouldRun {
			// we are now connected, set up OBS profiles initially
			if obsWriteAllProfiles(controllerIPAddress) {
				// forcibly restart OBS because we changed profile information
				log.Println("run: killing OBS because configuration changed")
				killOBS()
			}
			hasWrittenProfile = true

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
	}
}

func obsWriteAllProfiles(ipAddress string) bool {
	log.Println("obs: writing all profiles")
	var dirty bool
	dirty = obsWriteProfile("Primary", ipAddress, 1234) || dirty
	dirty = obsWriteProfile("Secondary", ipAddress, 1235) || dirty
	dirty = obsWriteScene("Untitled") || dirty
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

const sceneConfiguration string = "{\r\n    \"DesktopAudioDevice1\": {\r\n        \"deinterlace_field_order\": 0,\r\n        \"deinterlace_mode\": 0,\r\n        \"enabled\": true,\r\n        \"flags\": 0,\r\n        \"hotkeys\": {\r\n            \"libobs.mute\": [],\r\n            \"libobs.push-to-mute\": [],\r\n            \"libobs.push-to-talk\": [],\r\n            \"libobs.unmute\": []\r\n        },\r\n        \"id\": \"wasapi_output_capture\",\r\n        \"mixers\": 255,\r\n        \"monitoring_type\": 0,\r\n        \"muted\": false,\r\n        \"name\": \"Desktop Audio\",\r\n        \"private_settings\": {},\r\n        \"push-to-mute\": false,\r\n        \"push-to-mute-delay\": 0,\r\n        \"push-to-talk\": false,\r\n        \"push-to-talk-delay\": 0,\r\n        \"settings\": {\r\n            \"device_id\": \"default\"\r\n        },\r\n        \"sync\": 0,\r\n        \"volume\": 1.0\r\n    },\r\n    \"current_program_scene\": \"Scene\",\r\n    \"current_scene\": \"Scene\",\r\n    \"current_transition\": \"Fade\",\r\n    \"modules\": {\r\n        \"auto-scene-switcher\": {\r\n            \"active\": false,\r\n            \"interval\": 300,\r\n            \"non_matching_scene\": \"\",\r\n            \"switch_if_not_matching\": false,\r\n            \"switches\": []\r\n        },\r\n        \"captions\": {\r\n            \"enabled\": false,\r\n            \"lang_id\": 1033,\r\n            \"provider\": \"mssapi\",\r\n            \"source\": \"\"\r\n        },\r\n        \"output-timer\": {\r\n            \"autoStartRecordTimer\": false,\r\n            \"autoStartStreamTimer\": false,\r\n            \"recordTimerHours\": 0,\r\n            \"recordTimerMinutes\": 0,\r\n            \"recordTimerSeconds\": 30,\r\n            \"streamTimerHours\": 0,\r\n            \"streamTimerMinutes\": 0,\r\n            \"streamTimerSeconds\": 30\r\n        },\r\n        \"scripts-tool\": []\r\n    },\r\n    \"name\": \"Untitled\",\r\n    \"preview_locked\": false,\r\n    \"quick_transitions\": [\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 1,\r\n            \"name\": \"Cut\"\r\n        },\r\n        {\r\n            \"duration\": 300,\r\n            \"hotkeys\": [],\r\n            \"id\": 2,\r\n            \"name\": \"Fade\"\r\n        }\r\n    ],\r\n    \"saved_multiview_projectors\": [\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_multiview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_preview_projectors\": [\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"saved_projectors\": [\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        },\r\n        {\r\n            \"saved_projectors\": \"\"\r\n        }\r\n    ],\r\n    \"saved_studio_preview_projectors\": [\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        },\r\n        {\r\n            \"saved_studio_preview_projectors\": 0\r\n        }\r\n    ],\r\n    \"scaling_enabled\": false,\r\n    \"scaling_level\": 0,\r\n    \"scaling_off_x\": 0.0,\r\n    \"scaling_off_y\": 0.0,\r\n    \"scene_order\": [\r\n        {\r\n            \"name\": \"Scene\"\r\n        }\r\n    ],\r\n    \"sources\": [\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {\r\n                \"OBSBasic.SelectScene\": [],\r\n                \"libobs.hide_scene_item.Display Capture\": [],\r\n                \"libobs.show_scene_item.Display Capture\": []\r\n            },\r\n            \"id\": \"scene\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Scene\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"id_counter\": 1,\r\n                \"items\": [\r\n                    {\r\n                        \"align\": 5,\r\n                        \"bounds\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"bounds_align\": 0,\r\n                        \"bounds_type\": 0,\r\n                        \"crop_bottom\": 0,\r\n                        \"crop_left\": 0,\r\n                        \"crop_right\": 0,\r\n                        \"crop_top\": 0,\r\n                        \"id\": 1,\r\n                        \"locked\": false,\r\n                        \"name\": \"Display Capture\",\r\n                        \"pos\": {\r\n                            \"x\": 0.0,\r\n                            \"y\": 0.0\r\n                        },\r\n                        \"private_settings\": {},\r\n                        \"rot\": 0.0,\r\n                        \"scale\": {\r\n                            \"x\": 1.0,\r\n                            \"y\": 1.0\r\n                        },\r\n                        \"scale_filter\": \"disable\",\r\n                        \"visible\": true\r\n                    }\r\n                ]\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        },\r\n        {\r\n            \"deinterlace_field_order\": 0,\r\n            \"deinterlace_mode\": 0,\r\n            \"enabled\": true,\r\n            \"flags\": 0,\r\n            \"hotkeys\": {},\r\n            \"id\": \"monitor_capture\",\r\n            \"mixers\": 0,\r\n            \"monitoring_type\": 0,\r\n            \"muted\": false,\r\n            \"name\": \"Display Capture\",\r\n            \"private_settings\": {},\r\n            \"push-to-mute\": false,\r\n            \"push-to-mute-delay\": 0,\r\n            \"push-to-talk\": false,\r\n            \"push-to-talk-delay\": 0,\r\n            \"settings\": {\r\n                \"monitor\": 1\r\n            },\r\n            \"sync\": 0,\r\n            \"volume\": 1.0\r\n        }\r\n    ],\r\n    \"transition_duration\": 300,\r\n    \"transitions\": []\r\n}"

func obsWriteScene(name string) bool {
	sceneDir := filepath.Join(
		os.Getenv("APPDATA"),
		"obs-studio",
		"basic",
		"scenes",
	)
	os.MkdirAll(sceneDir, os.ModePerm)
	sceneJson := filepath.Join(sceneDir, fmt.Sprintf("%s.json", name))
	if data, err := ioutil.ReadFile(sceneJson); err != nil || string(data) != sceneConfiguration {
		log.Println("obs: writing scene configuration")
		ioutil.WriteFile(sceneJson, []byte(sceneConfiguration), os.ModePerm)
		return true
	}
	return false
}

func killOBS() {
	cmd := exec.Command("taskkill", "/f", "/t", "/im", "obs64.exe")
	cmd.Stdin = os.Stdin
	cmd.Stdout = os.Stdout
	cmd.Stderr = os.Stderr
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
		)
		cmd.Dir = filepath.Join(
			download.GetOBSInstallPath(),
			"bin",
			"64bit",
		)
		cmd.SysProcAttr = &syscall.SysProcAttr{
			// HideWindow: true,
		}
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
		[]byte("[General]\r\nLicenseAccepted=true\r\nFirstRun=true"),
		os.ModePerm,
	)
}
