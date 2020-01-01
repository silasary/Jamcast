package main

import (
	"fmt"
	"log"
	"net"

	jamcast "gitlab.com/redpointgames/jamcast/proto"
	"google.golang.org/grpc"
)

var end chan bool

type controllerServer struct {
}

func (s *controllerServer) Connect(req *jamcast.ClientRequest, srv jamcast.Controller_ConnectServer) error {

	// todo: actually implement; for now we just wait forever
	<-end

	return nil
}

func main() {
	end = make(chan bool)

	log.Println("controller: starting")

	lis, err := net.Listen("tcp", fmt.Sprintf(":%d", 8080))
	if err != nil {
		log.Fatalf("failed to listen: %v", err)
	}
	grpcServer := grpc.NewServer()
	jamcast.RegisterControllerServer(grpcServer, &controllerServer{})

	log.Println("controller: serving on port 8080")
	grpcServer.Serve(lis)

	log.Println("controller: exiting")
}
