package network

// #cgo CFLAGS: -std=c11 -w -DGOLANG_CGO=1
// #cgo LDFLAGS: -L${SRCDIR}/../libzt/windows-mingw-lib -lztunified -lstdc++ -lws2_32 -lShLwApi -liphlpapi
// #include "../libzt/windows-mingw-include/libzt.h"
import "C"

import (
	"os"
	"log"
	"path/filepath"

	"gitlab.com/redpointgames/jamcast/client/shutdown"
	"gitlab.com/redpointgames/jamcast/env"
)

type NetworkLayer struct {
	exit             chan bool
	shutdownComplete chan bool
}

func Start() *NetworkLayer {
	nl := &NetworkLayer{
		exit:             make(chan bool),
		shutdownComplete: make(chan bool),
	}

	go nl.run()

	return nl
}

func (nl *NetworkLayer) run() {
	ztpath := filepath.Join(env.GetAppPath(), "zt")
	os.MkdirAll(ztpath, 0644)
	
	C.zts_set_service_port(9102)
	
	log.Println("zerotier: starting")
	res := C.zts_startjoin(C.CString(ztpath), 0xb6079f73c66e4c60)
	if res != 0 {
		log.Println("zerotier: can't start")
		// todo
		// os.Exit(1)
	}
	log.Println("zerotier: started")

	shutdown.RegisterShutdownHandler(func() {
		nl.exit <- true
		<-nl.shutdownComplete
	})

	<-nl.exit

	log.Println("zerotier: shutting down")

	// C.zts_leave(0xb6079f73c66e4c60)
	C.zts_stop()

	log.Println("zerotier: shutdown complete")

	nl.shutdownComplete <- true
}
