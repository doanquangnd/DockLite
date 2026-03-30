// Package httpserver đăng ký toàn bộ route HTTP cho docklite-wsl.
package httpserver

import (
	"log"
	"net/http"
	"time"

	"docklite-wsl/internal/compose"
	"docklite-wsl/internal/docker"
	"docklite-wsl/internal/ws"
)

// ReadHeaderTimeout giá trị mặc định cho http.Server.ReadHeaderTimeout.
const ReadHeaderTimeout = 5 * time.Second

// Register gắn mọi handler REST + WebSocket vào mux.
func Register(mux *http.ServeMux) {
	mux.HandleFunc("/api/metrics", Metrics)
	mux.HandleFunc("/api/health", docker.Health)
	mux.HandleFunc("/api/docker/info", docker.DockerInfo)
	mux.HandleFunc("/api/containers/top-by-memory", docker.TopContainersByMemory)
	mux.HandleFunc("/api/containers/top-by-cpu", docker.TopContainersByCPU)
	mux.HandleFunc("/api/containers", docker.ContainersCollection)
	mux.HandleFunc("/api/containers/", docker.ContainersItem)
	mux.HandleFunc("/ws/containers/", ws.HandleLogs)
	compose.Register(mux)
	mux.HandleFunc("/api/images", docker.ImagesRoot)
	mux.HandleFunc("/api/images/prune", docker.ImagesPrune)
	mux.HandleFunc("/api/images/remove", docker.ImagesRemove)
	mux.HandleFunc("/api/system/prune", docker.SystemPrune)
}

// LogRequests middleware ghi method + path.
func LogRequests(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		IncRequestCount()
		log.Printf("%s %s", r.Method, r.URL.Path)
		next.ServeHTTP(w, r)
	})
}
