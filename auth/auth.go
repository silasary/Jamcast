package auth

import (
	"fmt"
	"io/ioutil"
	"log"
	"net"
	"net/http"
	"os"
	"os/exec"
	"path/filepath"
	"runtime"
	"strings"

	"github.com/dgrijalva/jwt-go"
	"gitlab.com/redpointgames/jamcast/env"
)

func HasCredentials() bool {
	token, err := loadToken()
	if token != nil && err == nil {
		return true
	}
	if err != nil {
		log.Printf("auth: error while loading existing token: %v\n", err)
	}
	return false
}

func GetCredentials() (*jwt.Token, error) {
	log.Println("auth: credentials requested")

	existingToken, err := loadToken()
	if existingToken != nil && err == nil {
		log.Println("auth: provided existing token")
		return existingToken, nil
	}

	var srv *http.Server
	srv = &http.Server{
		Addr: ":0",
		Handler: http.HandlerFunc(func(w http.ResponseWriter, req *http.Request) {
			log.Printf("auth: got HTTP request at %s %s\n", req.Method, req.URL)

			tokenRaw := req.URL.Query().Get("token")
			if tokenRaw == "" {
				writeResponse(w, "No token provided in request.")
				return
			}

			token, err := readAndVerifyToken(tokenRaw)
			if err != nil {
				writeResponse(w, fmt.Sprintf("Invalid token provided (did not pass checks): %v", err))
				return
			}

			err = saveToken(token)
			if err != nil {
				writeResponse(w, fmt.Sprintf("Unable to save authentication credentials: %v", err))
				return
			}

			// no need to reload from disk after saving it
			existingToken = token

			writeResponse(w, "Success! You can now close this window.")
			go func() {
				srv.Shutdown(req.Context())
			}()
		}),
	}

	socket, err := net.Listen("tcp", "127.0.0.1:0")
	if err != nil {
		return nil, err
	}

	_, port, err := net.SplitHostPort(socket.Addr().String())
	if err != nil {
		return nil, err
	}

	log.Printf("auth: listening on port %s\n", port)

	url := fmt.Sprintf("%s/jamcast/auth?port=%s", getJamHost(), port)
	log.Printf("auth: launching web browser at URL %s\n", url)
	openBrowser(url)

	err = srv.Serve(socket)
	if err != nil && !strings.Contains(err.Error(), "Server closed") {
		return nil, err
	}
	if existingToken == nil {
		return nil, fmt.Errorf("expected to have token at this point")
	}

	log.Println("auth: returned token to caller")
	return existingToken, nil
}

func saveToken(token *jwt.Token) error {
	return ioutil.WriteFile(filepath.Join(env.GetAppPath(), "token"), []byte(token.Raw), 0600)
}

func loadToken() (*jwt.Token, error) {
	tokenRaw, err := ioutil.ReadFile(filepath.Join(env.GetAppPath(), "token"))
	if os.IsNotExist(err) {
		return nil, nil
	}
	token, err := readAndVerifyToken(string(tokenRaw))
	if err != nil {
		return nil, err
	}
	return token, err
}

func readAndVerifyToken(tokenRaw string) (*jwt.Token, error) {
	token, err := jwt.Parse(tokenRaw, func(token *jwt.Token) (interface{}, error) {
		if _, ok := token.Method.(*jwt.SigningMethodRSA); !ok {
			return nil, fmt.Errorf("unexpected signing method: %v", token.Header["alg"])
		}

		verifyKey, err := jwt.ParseRSAPublicKeyFromPEM([]byte("-----BEGIN PUBLIC KEY-----\nMFwwDQYJKoZIhvcNAQEBBQADSwAwSAJBAMlxpgQQTWy3Pv1sahWkSnIcu3Gch7o6\nN7yWgjIssudXy7dflBy4BkhaEBu1MU3yPPmYqG/uGy8rDuVS7yHHY2sCAwEAAQ==\n-----END PUBLIC KEY-----"))
		if err != nil {
			return nil, fmt.Errorf("error while loading verification key: %v", err)
		}

		return verifyKey, nil
	})
	return token, err
}

func getJamHost() string {
	if os.Getenv("JAMHOST") != "" {
		return os.Getenv("JAMHOST")
	}
	return "https://beta.jamhost.org"
}

func openBrowser(url string) error {
	switch runtime.GOOS {
	case "linux":
		return exec.Command("xdg-open", url).Start()
	case "windows":
		return exec.Command("rundll32", "url.dll,FileProtocolHandler", url).Start()
	case "darwin":
		return exec.Command("open", url).Start()
	default:
		return fmt.Errorf("unsupported platform")
	}
}

func writeResponse(w http.ResponseWriter, message string) {
	w.Header().Set("Content-Type", "text/html")
	w.WriteHeader(200)
	w.Write([]byte("<html><head>"))
	w.Write([]byte("<title>JamCast Authentication</title>"))
	w.Write([]byte("<script>window.top.postMessage('done', '*');</script>"))
	w.Write([]byte("<link rel=\"stylesheet\" href=\"https://stackpath.bootstrapcdn.com/bootstrap/4.4.1/css/bootstrap.min.css\" integrity=\"sha384-Vkoo8x4CGsO3+Hhxv8T/Q5PaXtkKtu6ug5TOeNV6gBiFeWPGFN9MuhOf23Q9Ifjh\" crossorigin=\"anonymous\">"))
	w.Write([]byte("</head><body class=\"p-2\">"))
	w.Write([]byte(fmt.Sprintf("<p>%s</p>", message)))
	w.Write([]byte("</body></html>"))
}
