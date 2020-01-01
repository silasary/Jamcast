package shutdown

import (
	"log"
	"os"
	"os/signal"
	"sync"
	"syscall"
)

var shutdownHandlers []func()
var shutdownMutex sync.Mutex

func RegisterShutdownHandler(onShutdown func()) {
	shutdownMutex.Lock()
	defer shutdownMutex.Unlock()

	shutdownHandlers = append(shutdownHandlers, onShutdown)
}

func SetupShutdownGlobalHandler() {
	c := make(chan os.Signal, 2)
	signal.Notify(c, os.Interrupt, syscall.SIGTERM)
	go func() {
		<-c
		Shutdown()
	}()
}

func Shutdown() {
	log.Println("shutdown: shutting down")

	for _, handler := range shutdownHandlers {
		handler()
	}

	log.Println("shutdown: shut down complete")
	os.Exit(0)
}
