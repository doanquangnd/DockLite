package main

import (
	"bytes"
	"encoding/json"
	"log"
	"net/http"
	"os"
	"os/exec"
	"strings"
	"time"
)

type healthResponse struct {
	Status  string `json:"status"`
	Service string `json:"service"`
	Version string `json:"version"`
}

func main() {
	// 0.0.0.0 giúp Windows (localhost:17890) forward vào WSL ổn định hơn so với chỉ 127.0.0.1 trong một số cấu hình WSL2.
	addr := "0.0.0.0:17890"
	if v := os.Getenv("DOCKLITE_ADDR"); v != "" {
		addr = v
	}

	mux := http.NewServeMux()
	mux.HandleFunc("/api/health", healthHandler)
	mux.HandleFunc("/api/docker/info", dockerInfoHandler)
	mux.HandleFunc("/api/containers", containersCollectionHandler)
	mux.HandleFunc("/api/containers/", containersItemHandler)
	mux.HandleFunc("/ws/containers/", wsLogsHandler)
	mux.HandleFunc("/api/compose/projects", composeProjectsCollectionHandler)
	mux.HandleFunc("/api/compose/projects/", composeProjectItemHandler)
	mux.HandleFunc("/api/compose/up", composeUpHandler)
	mux.HandleFunc("/api/compose/down", composeDownHandler)
	mux.HandleFunc("/api/compose/ps", composePsHandler)
	mux.HandleFunc("/api/images", imagesRootHandler)
	mux.HandleFunc("/api/images/prune", imagesPruneHandler)
	mux.HandleFunc("/api/images/remove", imagesRemoveHandler)
	mux.HandleFunc("/api/system/prune", systemPruneHandler)

	srv := &http.Server{
		Addr:              addr,
		Handler:           logRequests(mux),
		ReadHeaderTimeout: 5 * time.Second,
	}

	log.Printf("docklite-wsl lắng nghe %s (REST + WS + compose + images + prune)", addr)
	if err := srv.ListenAndServe(); err != nil && err != http.ErrServerClosed {
		log.Fatal(err)
	}
}

func healthHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	resp := healthResponse{Status: "ok", Service: "docklite-wsl", Version: "0.1.0"}
	_ = json.NewEncoder(w).Encode(resp)
}

func dockerInfoHandler(w http.ResponseWriter, r *http.Request) {
	if r.Method != http.MethodGet {
		http.Error(w, "method not allowed", http.StatusMethodNotAllowed)
		return
	}
	ctx := r.Context()
	cmd := exec.CommandContext(ctx, "docker", "info", "-f", "{{json .}}")
	var stdout, stderr bytes.Buffer
	cmd.Stdout = &stdout
	cmd.Stderr = &stderr
	err := cmd.Run()
	w.Header().Set("Content-Type", "application/json; charset=utf-8")
	if err != nil {
		msg := strings.TrimSpace(stderr.String())
		if msg == "" {
			msg = err.Error()
		}
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": msg})
		return
	}
	var src struct {
		ServerVersion     string `json:"ServerVersion"`
		Containers        int    `json:"Containers"`
		ContainersRunning int    `json:"ContainersRunning"`
		Images            int    `json:"Images"`
		OSType            string `json:"OSType"`
		KernelVersion     string `json:"KernelVersion"`
		OperatingSystem   string `json:"OperatingSystem"`
	}
	if err := json.Unmarshal(stdout.Bytes(), &src); err != nil {
		_ = json.NewEncoder(w).Encode(map[string]interface{}{"ok": false, "error": "json: " + err.Error()})
		return
	}
	_ = json.NewEncoder(w).Encode(map[string]interface{}{
		"ok":                true,
		"serverVersion":     src.ServerVersion,
		"containers":        src.Containers,
		"containersRunning": src.ContainersRunning,
		"images":            src.Images,
		"osType":            src.OSType,
		"kernelVersion":     src.KernelVersion,
		"operatingSystem":   src.OperatingSystem,
	})
}

func logRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		log.Printf("%s %s", r.Method, r.URL.Path)
		next.ServeHTTP(w, r)
	})
}
